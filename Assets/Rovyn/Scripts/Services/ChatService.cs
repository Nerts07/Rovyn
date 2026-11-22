using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

namespace Rovyn
{
    // Text messaging through Vivox: direct messages between players and group text channels.
    // Messages only reach the UI through Vivox events or history queries. Sent messages show up
    // when Vivox echoes them back, so nothing local is ever passed off as delivered.
    //
    // Conversations are identified by a key: "dm:<playerId>" or "ch:<vivoxChannelName>".
    public sealed class ChatService
    {
        public event Action<string, VivoxMessage> MessageReceived;
        internal event Action<string> MessageRemoved;
        public event Action UnreadChanged;
        public event Action DirectListChanged;

        public string OpenKey { get; private set; }
        public IReadOnlyList<string> DirectPartners => _partners;

        static IVivoxService Service => VivoxService.Instance;

        readonly List<string> _partners = new();        // most recent first
        readonly Dictionary<string, int> _unread = new();
        readonly Dictionary<string, VivoxMessage> _lastIncoming = new();
        readonly Dictionary<string, HashSet<string>> _hidden = new();
        readonly HashSet<string> _deleted = new(StringComparer.Ordinal);
        readonly HashSet<(string player, string message)> _deleting = new();
        string _lastDirectTarget;
        string _textChannel;

        public static string DirectKey(string playerId) => "dm:" + playerId;
        public static string ChannelKey(string channel) => "ch:" + channel;
        public static bool IsDirect(string key) => key != null && key.StartsWith("dm:");
        public static string IdOf(string key) => key?.Substring(3);

        public void Start()
        {
            Service.DirectedMessageReceived += OnDirectMessage;
            Service.ChannelMessageReceived += OnChannelMessage;
            Service.ChannelMessageDeleted += OnMessageDeleted;
            Service.DirectedMessageDeleted += OnMessageDeleted;
            App.Vivox.LoggedIn += OnLoggedIn;
        }

        public void Stop()
        {
            Service.DirectedMessageReceived -= OnDirectMessage;
            Service.ChannelMessageReceived -= OnChannelMessage;
            Service.ChannelMessageDeleted -= OnMessageDeleted;
            Service.DirectedMessageDeleted -= OnMessageDeleted;
            App.Vivox.LoggedIn -= OnLoggedIn;
            _partners.Clear();
            _unread.Clear();
            _lastIncoming.Clear();
            _hidden.Clear();
            _deleted.Clear();
            _textChannel = null;
            OpenKey = null;
            DirectListChanged?.Invoke();
            UnreadChanged?.Invoke();
        }

        // ---- Unread ----

        public int UnreadCount(string key) => _unread.TryGetValue(key, out var n) ? n : 0;
        public int TotalDirectUnread => _unread.Where(p => IsDirect(p.Key)).Sum(p => p.Value);

        // The conversation on screen. Its incoming messages are marked read right away.
        public void SetOpen(string key)
        {
            OpenKey = key;
            if (key == null) return;
            if (IsDirect(key)) AddPartner(IdOf(key));
            MarkReadAsync(key).Forget();
        }

        async UniTaskVoid MarkReadAsync(string key)
        {
            if (_unread.Remove(key)) UnreadChanged?.Invoke();
            if (!_lastIncoming.TryGetValue(key, out var last) || last.IsRead) return;
            try { await Service.SetMessageAsReadAsync(last); }
            catch (Exception e) { Debug.LogWarning($"Mark as read failed: {e.Message}"); }
        }

        // ---- Text channels ----

        // Only one text channel is joined at a time; switching leaves the previous one.
        public async UniTask JoinTextChannelAsync(string channel)
        {
            if (_textChannel == channel && Service.ActiveChannels.ContainsKey(channel)) return;
            var previous = _textChannel;
            _textChannel = channel;
            if (previous != null && Service.ActiveChannels.ContainsKey(previous))
            {
                try { await Service.LeaveChannelAsync(previous); }
                catch (Exception e) { Debug.LogWarning($"Leaving {previous} failed: {e.Message}"); }
            }
            if (!Service.ActiveChannels.ContainsKey(channel))
                await Service.JoinGroupChannelAsync(channel, ChatCapability.TextOnly);
        }

        // ---- Messages ----

        // History comes back oldest first. Pass `before` to page further back.
        public async UniTask<ReadOnlyCollection<VivoxMessage>> LoadHistoryAsync(string key, DateTime? before, int count = 50)
        {
            var options = new ChatHistoryQueryOptions { TimeEnd = before };
            var messages = IsDirect(key)
                ? await Service.GetDirectTextMessageHistoryAsync(IdOf(key), count, options)
                : await Service.GetChannelTextMessageHistoryAsync(IdOf(key), count, options);
            if (messages == null) throw new InvalidOperationException("Chat history is unavailable.");

            var lastIncoming = messages.LastOrDefault(m => !m.FromSelf);
            if (lastIncoming != null && (!_lastIncoming.TryGetValue(key, out var known) || known.ReceivedTime < lastIncoming.ReceivedTime))
                _lastIncoming[key] = lastIncoming;
            if (key == OpenKey) MarkReadAsync(key).Forget();
            return messages;
        }

        public UniTask SendAsync(string key, string text) => SendAsync(key, ChatMetadata.EscapeBody(text), null);

        internal UniTask SendReplyAsync(string key, string text, ChatMetadata reply) =>
            reply == null ? SendAsync(key, text) : SendAsync(key, reply.AddReplyTrailer(text), reply.AsReply());

        internal UniTask SendReactionAsync(string key, string target, string emoji, bool added) =>
            SendAsync(key, ChatMetadata.ReactionBody(target, emoji, added), null, reaction: true);

        internal bool IsMessageRemoved(string key, string id) => !string.IsNullOrEmpty(id) &&
            (_deleted.Contains(id) || HiddenMessages(key).Contains(id));

        internal void HideMessage(string key, string id)
        {
            var hidden = HiddenMessages(key);
            if (!hidden.Add(id)) return;
            try
            {
                PlayerPrefs.SetString(HiddenMessagesKey(key), string.Join("\n", hidden));
                PlayerPrefs.Save();
            }
            catch
            {
                hidden.Remove(id);
                throw;
            }
            MessageRemoved?.Invoke(id);
        }

        HashSet<string> HiddenMessages(string key)
        {
            if (!_hidden.TryGetValue(key, out var hidden))
            {
                hidden = new HashSet<string>(PlayerPrefs.GetString(HiddenMessagesKey(key))
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
                _hidden.Add(key, hidden);
            }
            return hidden;
        }

        static string HiddenMessagesKey(string key) => $"rovyn.hidden_messages.{App.Auth.PlayerId}.{key}";

        internal async UniTask DeleteOwnMessageAsync(string key, string id)
        {
            if (App.Vivox.State != ConnectionState.Connected)
                throw new InvalidOperationException("Reconnect to chat to delete this message.");

            var pending = (App.Auth.PlayerId, id);
            if (!_deleting.Add(pending)) return;
            try
            {
                if (IsDirect(key)) await Service.DeleteDirectTextMessageAsync(id);
                else await Service.DeleteChannelTextMessageAsync(IdOf(key), id);
                if (App.SessionActive && pending.Item1 == App.Auth.PlayerId) RegisterDeletion(id);
            }
            finally { _deleting.Remove(pending); }
        }

        void OnMessageDeleted(VivoxMessage message) => RegisterDeletion(message.MessageId);

        void RegisterDeletion(string id)
        {
            if (!string.IsNullOrEmpty(id) && _deleted.Add(id)) MessageRemoved?.Invoke(id);
        }

        async UniTask SendAsync(string key, string text, MessageOptions options, bool reaction = false)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (!App.Vivox.IsLoggedIn) throw new InvalidOperationException("Not connected to chat.");

            if (IsDirect(key))
            {
                var partner = IdOf(key);
                _lastDirectTarget = partner;
                await Service.SendDirectTextMessageAsync(partner, text, options);
                if (!reaction) AddPartner(partner);
            }
            else
            {
                await Service.SendChannelTextMessageAsync(IdOf(key), text, options);
            }
        }

        void OnDirectMessage(VivoxMessage m)
        {
            // Vivox echoes our own DMs back with the server-assigned ID. Older servers may leave
            // out the recipient on the echo, so fall back to the last player we wrote to.
            var partner = m.FromSelf ? (m.RecipientPlayerId ?? _lastDirectTarget) : m.SenderPlayerId;
            if (string.IsNullOrEmpty(partner)) return;
            if (!ChatMetadata.IsReaction(m)) AddPartner(partner);
            Deliver(DirectKey(partner), m);
        }

        void OnChannelMessage(VivoxMessage m) => Deliver(ChannelKey(m.ChannelName), m);

        void Deliver(string key, VivoxMessage m)
        {
            if (IsMessageRemoved(key, m.MessageId)) return;
            if (!m.FromSelf)
            {
                _lastIncoming[key] = m;
                if (key == OpenKey) MarkReadAsync(key).Forget();
                else if (!ChatMetadata.IsReaction(m))
                {
                    _unread[key] = UnreadCount(key) + 1;
                    UnreadChanged?.Invoke();
                }
            }
            MessageReceived?.Invoke(key, m);
        }

        // ---- Direct message list ----

        public void AddPartner(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == App.Auth.PlayerId) return;
            if (_partners.Count > 0 && _partners[0] == playerId) return;
            _partners.Remove(playerId);
            _partners.Insert(0, playerId);
            DirectListChanged?.Invoke();
        }

        public void RemovePartner(string playerId)
        {
            if (_partners.Remove(playerId)) DirectListChanged?.Invoke();
        }

        void OnLoggedIn() => LoadConversationsAsync().Forget();

        // Rebuilds the DM list from Vivox's stored conversations and works out unread counts
        // from the read flags on recent history.
        async UniTaskVoid LoadConversationsAsync()
        {
            try
            {
                var conversations = await Service.GetConversationsAsync(new ConversationQueryOptions { PageSize = 20 });
                foreach (var c in conversations.Where(c => c.ConversationType == ConversationType.DirectedMessageConversation).Reverse())
                    AddPartner(c.Name);

                foreach (var partner in _partners.ToList())
                {
                    var key = DirectKey(partner);
                    var history = await Service.GetDirectTextMessageHistoryAsync(partner, 20);
                    var last = history.LastOrDefault(m => !m.FromSelf);
                    if (last != null) _lastIncoming[key] = last;
                    int unread = history.Count(m => !m.FromSelf && !m.IsRead && !ChatMetadata.IsReaction(m)
                        && !IsMessageRemoved(key, m.MessageId));
                    if (unread > 0 && key != OpenKey) _unread[key] = unread;
                }
                UnreadChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Loading conversations failed: {e.Message}");
            }
        }
    }
}
