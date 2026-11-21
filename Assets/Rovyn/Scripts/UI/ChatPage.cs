using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Conversation view for both direct messages and server text channels.
    public class ChatPage : MonoBehaviour
    {
        [Header("Header")]
        public Image headerIcon;        // "#" for channels
        public Avatar headerAvatar;     // the other person, for direct messages
        public TMP_Text title;
        public TMP_Text topic;
        public Button callButton;
        public Button profileButton;

        [Header("Messages")]
        public ScrollRect scroll;
        public RectTransform content;
        public MessageItem messagePrefab;
        public GameObject welcome;
        public Avatar welcomeAvatar;
        public GameObject welcomeChannelIcon;
        public TMP_Text welcomeTitle;
        public TMP_Text welcomeText;
        public GameObject loading;

        [Header("Composer")]
        public TMP_InputField composer;
        public TMP_Text placeholder;
        UnityEngine.UI.Button _sendButton;
        UnityEngine.UI.Button _emojiButton;
        EmojiPicker _emojiPicker;
        Sprite _shape;
        GameObject _replyBar;
        TMP_Text _replyCaption;
        UnityEngine.UI.LayoutElement _composerSize;
        ChatMetadata _reply;

        const int PageSize = 50;
        const float ComposerHeight = 68f;
        const float ReplyHeight = 30f;
        internal const int QuickReactionCount = 3;
        static readonly string[] DefaultQuickReactions = { "❤️", "😂", "👍" };

        string _key;
        string _peerId;     // set for direct messages
        string _channelId;  // set for text channels
        string _label;      // "@name" or "#channel"

        readonly List<MessageItem> _items = new();
        readonly Dictionary<string, MessageItem> _itemsById = new();
        readonly List<VivoxMessage> _arrivedWhileLoading = new();
        readonly ReactionHistory _reactions = new();
        readonly HashSet<(string target, string emoji)> _sendingReactions = new();
        DateTime? _historyBefore;
        bool _loaded;
        bool _loadingOlder;
        bool _reachedStart;
        int _version;
        int _jumpVersion;

        void Awake()
        {
            const float sendWidth = 64f;
            const float emojiWidth = 54f;
            _sendButton = UiPresentation.AddFieldAction(composer, "Send", sendWidth, true);
            _emojiButton = UiPresentation.AddFieldAction(composer, "Emoji", emojiWidth, false);
            _emojiButton.onClick.AddListener(PickComposerEmoji);
            ((RectTransform)_emojiButton.transform).anchoredPosition += new Vector2(-sendWidth - 4, 0);
            var viewport = composer.textViewport;
            viewport.offsetMax = new Vector2(-sendWidth - emojiWidth - 18f, viewport.offsetMax.y);
            composer.onFocusSelectAll = false;
            composer.resetOnDeActivation = false;
            composer.richText = false;
            _sendButton.onClick.AddListener(Send);
            composer.onValueChanged.AddListener(text =>
            {
                RefreshSendButton();
                EmojiCatalog.AttachComposer(composer.textComponent, text);
            });
            composer.onSubmit.AddListener(_ => Send());
            RefreshSendButton();

            var background = (UnityEngine.UI.Image)composer.targetGraphic;
            _shape = background.sprite;
            UiPresentation.RoundSurface(background, background.sprite, 10f);
            var composerWrap = composer.transform.parent;
            _composerSize = composerWrap.GetComponent<UnityEngine.UI.LayoutElement>();
            _composerSize.preferredHeight = ComposerHeight;
            composerWrap.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().padding = new RectOffset(16, 16, 0, 18);
            BuildReplyBar(composerWrap);

            callButton.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
            welcomeTitle.richText = false;
            welcomeText.richText = false;
            welcomeTitle.fontSize = 24f;
            scroll.viewport.gameObject.AddComponent<ChatHistoryViewport>().Configure(scroll);
            scroll.onValueChanged.AddListener(OnScroll);
            callButton.onClick.AddListener(() => MainScreen.Instance.StartCall(_peerId));
            profileButton.onClick.AddListener(() => MainScreen.Instance.ShowProfile(_peerId, fromChat: true));
            App.Chat.MessageReceived += OnMessage;
        }

        void OnEnable()
        {
            App.Vivox.StateChanged += OnConnectionChanged;
            App.Friends.PresenceChanged += OnPresence;
            App.Friends.Changed += RefreshHeader;
            RefreshHeader();
        }

        void OnDisable()
        {
            App.Vivox.StateChanged -= OnConnectionChanged;
            App.Friends.PresenceChanged -= OnPresence;
            App.Friends.Changed -= RefreshHeader;
            if (_emojiPicker != null) _emojiPicker.Close();
        }

        void OnDestroy() => App.Chat.MessageReceived -= OnMessage;

        // ---- Opening conversations ----

        public void OpenDirect(string playerId)
        {
            var key = ChatService.DirectKey(playerId);
            if (key == _key && _loaded) { App.Chat.SetOpen(key); FocusComposer(); return; }

            _peerId = playerId;
            _channelId = null;
            var name = App.Friends.NameOf(playerId);
            _label = "@" + name;

            welcomeAvatar.gameObject.SetActive(true);
            welcomeAvatar.Set(playerId, name);
            welcomeChannelIcon.SetActive(false);
            welcomeTitle.text = name;
            welcomeText.text = $"Your conversation with {name} starts here.";
            Begin(key);
        }

        public void OpenChannel(ServerCatalog.Server server, string channel)
        {
            var id = ServerCatalog.TextChannelId(server, channel);
            var key = ChatService.ChannelKey(id);
            if (key == _key && _loaded) { App.Chat.SetOpen(key); FocusComposer(); return; }

            _peerId = null;
            _channelId = id;
            _label = "#" + channel;

            welcomeAvatar.gameObject.SetActive(false);
            welcomeChannelIcon.SetActive(true);
            welcomeTitle.text = $"#{channel}";
            welcomeText.text = $"Start of the conversation in {server.name}.";
            Begin(key);
        }

        void Begin(string key)
        {
            Clear();
            _key = key;
            App.Chat.SetOpen(key);
            RefreshHeader();
            LoadAsync(_version).Forget();
            FocusComposer();
        }

        // Drops all message items and cancels any load in flight.
        public void Clear()
        {
            _version++;
            scroll.StopMovement();
            foreach (var item in _items) Destroy(item.gameObject);
            _items.Clear();
            _itemsById.Clear();
            _arrivedWhileLoading.Clear();
            _reactions.Clear();
            _sendingReactions.Clear();
            _historyBefore = null;
            _loaded = false;
            _loadingOlder = false;
            _reachedStart = false;
            welcome.SetActive(false);
            loading.SetActive(false);
            _key = null;
            SetReply(null);
            if (_emojiPicker != null) _emojiPicker.Close();
        }

        // ---- Loading ----

        async UniTaskVoid LoadAsync(int version)
        {
            if (!App.Vivox.IsLoggedIn) return; // OnConnectionChanged retries after login
            loading.SetActive(true);
            try
            {
                if (_channelId != null) await App.Chat.JoinTextChannelAsync(_channelId);
                if (version != _version) return;
                var history = await FetchVisiblePageAsync(version);
                if (version != _version) return;

                foreach (var m in history) Append(m);
                foreach (var m in _arrivedWhileLoading) AcceptMessage(m);
                _arrivedWhileLoading.Clear();

                RefreshQuotes();
                welcome.SetActive(_reachedStart);
                _loaded = true;
                ScrollToBottomAsync().Forget();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (version == _version) App.Report("Couldn't load messages. Try again in a moment.");
            }
            finally
            {
                if (version == _version) loading.SetActive(false);
            }
        }

        async UniTask<bool> LoadOlderAsync()
        {
            if (_loadingOlder || _reachedStart || !_loaded) return false;
            _loadingOlder = true;
            int version = _version;
            try
            {
                var older = await FetchVisiblePageAsync(version);
                if (version != _version) return false;
                welcome.SetActive(_reachedStart);

                // Keep the messages on screen in place while older ones are inserted above.
                float heightBefore = content.rect.height;
                for (int i = older.Count - 1; i >= 0; i--) Prepend(older[i]);
                RefreshQuotes();
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                content.anchoredPosition += new Vector2(0f, content.rect.height - heightBefore);
                return older.Count > 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Loading older messages failed: {e.Message}");
                return false;
            }
            finally
            {
                if (version == _version) _loadingOlder = false;
            }
        }

        async UniTask<List<VivoxMessage>> FetchVisiblePageAsync(int version)
        {
            var key = _key;
            var visible = new List<VivoxMessage>();
            var collected = new HashSet<string>();
            var cursor = _historyBefore;
            bool reachedStart = _reachedStart;
            while (!reachedStart && visible.Count < PageSize)
            {
                var page = await App.Chat.LoadHistoryAsync(key, cursor, PageSize);
                if (version != _version) return visible;
                if (page.Count == 0)
                {
                    reachedStart = true;
                    break;
                }

                foreach (var message in page)
                {
                    if (ChatMetadata.IsReaction(message))
                        ApplyReaction(message);
                    else if (!string.IsNullOrEmpty(message.MessageId) &&
                        !_itemsById.ContainsKey(message.MessageId) && collected.Add(message.MessageId))
                        visible.Add(message);
                }

                var oldest = page.Min(message => message.ReceivedTime);
                reachedStart = page.Count < PageSize || (cursor.HasValue && oldest >= cursor.Value);
                cursor = oldest;
            }
            _historyBefore = cursor;
            _reachedStart = reachedStart;
            visible.Sort((left, right) => left.ReceivedTime.CompareTo(right.ReceivedTime));
            return visible;
        }

        void OnScroll(Vector2 position)
        {
            bool scrollable = content.rect.height > scroll.viewport.rect.height + 0.01f;
            if (scrollable && position.y > 0.98f) LoadOlderAsync().Forget();
        }

        // ---- Message items ----

        void OnMessage(string key, VivoxMessage message)
        {
            if (key != _key) return;
            if (!_loaded)
            {
                _arrivedWhileLoading.Add(message);
                return;
            }
            bool atBottom = IsAtBottom();
            bool reaction = ChatMetadata.IsReaction(message);
            AcceptMessage(message);
            if (isActiveAndEnabled && (atBottom || (!reaction && message.FromSelf))) ScrollToBottomAsync().Forget();
        }

        void AcceptMessage(VivoxMessage message)
        {
            if (ChatMetadata.IsReaction(message)) ApplyReaction(message);
            else Append(message);
        }

        void ApplyReaction(VivoxMessage message)
        {
            var target = _reactions.Apply(message);
            if (target != null && _itemsById.TryGetValue(target, out var item))
                item.SetReactions(_reactions.For(target, App.Auth.PlayerId));
        }

        MessageItem CreateItem(VivoxMessage message)
        {
            if (string.IsNullOrEmpty(message.MessageId) || _itemsById.ContainsKey(message.MessageId)) return null;
            var item = Instantiate(messagePrefab, content, false);
            try
            {
                item.Attach(this, _shape);
                item.Set(message);
                item.SetReactions(_reactions.For(message.MessageId, App.Auth.PlayerId));
                if (item.Reply != null && _itemsById.TryGetValue(item.Reply.target, out var original))
                    item.UpdateQuote(original);
            }
            catch
            {
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
                throw;
            }
            _itemsById.Add(message.MessageId, item);
            return item;
        }

        void RefreshQuotes()
        {
            foreach (var item in _items)
                if (item.Reply != null && _itemsById.TryGetValue(item.Reply.target, out var original))
                    item.UpdateQuote(original);
        }

        void Append(VivoxMessage message)
        {
            var item = CreateItem(message);
            if (item == null) return;
            item.SetHeader(!item.ContinuesFrom(_items.Count > 0 ? _items[_items.Count - 1] : null));
            _items.Add(item);
        }

        void Prepend(VivoxMessage message)
        {
            var item = CreateItem(message);
            if (item == null) return;
            item.transform.SetSiblingIndex(welcome.transform.GetSiblingIndex() + 1);
            item.SetHeader(true);
            _items.Insert(0, item);
            if (_items.Count > 1) _items[1].SetHeader(!_items[1].ContinuesFrom(item));
        }

        bool IsAtBottom() =>
            content.rect.height <= scroll.viewport.rect.height + 0.01f || scroll.verticalNormalizedPosition <= 0.01f;

        async UniTaskVoid ScrollToBottomAsync()
        {
            int version = _version;
            // Wait for the layout pass so the new content height is known.
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            if (this == null || version != _version) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 0f;
        }

        internal void ReplyTo(MessageItem item)
        {
            if (!composer.interactable || !_itemsById.ContainsKey(item.MessageId)) return;
            SetReply(new ChatMetadata
            {
                target = item.MessageId,
                author = ChatMetadata.Preview(item.author.text, 48),
                excerpt = ChatMetadata.Preview(item.PlainText, 140),
            });
            FocusComposer();
        }

        internal void OpenOriginal(ChatMetadata reply)
        {
            if (reply != null) JumpToAsync(reply.target).Forget();
        }

        async UniTaskVoid JumpToAsync(string target)
        {
            int version = _version;
            int jump = ++_jumpVersion;
            while (!_itemsById.ContainsKey(target) && !_reachedStart && version == _version && jump == _jumpVersion)
            {
                if (_loadingOlder)
                    await UniTask.WaitUntil(() => !_loadingOlder || version != _version);
                else if (!await LoadOlderAsync()) break;
            }
            if (version != _version || jump != _jumpVersion || !isActiveAndEnabled) return;
            if (!_itemsById.TryGetValue(target, out var item))
            {
                App.Report("The original message is unavailable.");
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var row = (RectTransform)item.transform;
            float distance = content.rect.yMax - content.InverseTransformPoint(
                row.TransformPoint(new Vector3(0, row.rect.yMax, 0))).y;
            float range = content.rect.height - scroll.viewport.rect.height;
            scroll.StopMovement();
            if (range > 0.01f)
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((distance - 12) / range);
            item.HighlightOriginal();
        }

        internal void PickReaction(MessageItem item)
        {
            if (!composer.interactable || !_itemsById.ContainsKey(item.MessageId)) return;
            ShowEmojiPicker(emoji => ToggleReaction(item, emoji));
        }

        internal void ToggleReaction(MessageItem item, string emoji)
        {
            if (!composer.interactable || _key == null || !_itemsById.ContainsKey(item.MessageId)) return;
            var key = (item.MessageId, emoji);
            if (_sendingReactions.Add(key)) SendReactionAsync(key).Forget();
        }

        async UniTaskVoid SendReactionAsync((string target, string emoji) reaction)
        {
            int version = _version;
            var playerId = App.Auth.PlayerId;
            bool added = !_reactions.Has(reaction.target, reaction.emoji, playerId);
            try
            {
                await App.Chat.SendReactionAsync(_key, reaction.target, reaction.emoji, added);
                if (added && !string.IsNullOrEmpty(playerId))
                {
                    var recent = ReadRecentReactions(playerId).Prepend(reaction.emoji)
                        .Distinct(StringComparer.Ordinal).Take(QuickReactionCount);
                    PlayerPrefs.SetString(RecentReactionsKey(playerId), string.Join("\n", recent));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (version == _version) App.Report("Couldn't update the reaction.");
            }
            finally
            {
                if (version == _version) _sendingReactions.Remove(reaction);
            }
        }

        internal string[] QuickReactions() => ReadRecentReactions(App.Auth.PlayerId)
            .Concat(DefaultQuickReactions).Distinct(StringComparer.Ordinal).Take(QuickReactionCount).ToArray();

        static IEnumerable<string> ReadRecentReactions(string playerId) =>
            PlayerPrefs.GetString(RecentReactionsKey(playerId)).Split('\n')
                .Select(EmojiCatalog.Canonical).Where(emoji => emoji != null);

        static string RecentReactionsKey(string playerId) => $"rovyn.recent_reactions.{playerId}";

        void BuildReplyBar(Transform parent)
        {
            var bar = ChatWidgets.Rect(parent, "Reply Draft");
            bar.SetAsFirstSibling();
            ChatWidgets.Size(bar, -1, ReplyHeight);
            var row = bar.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            row.spacing = 6f;
            var font = composer.textComponent.font;
            var labelRect = ChatWidgets.Rect(bar, "Reply Preview");
            _replyCaption = ChatWidgets.Label(labelRect, font, 13, Theme.TextMuted);
            ChatWidgets.Size(labelRect, -1, ReplyHeight).flexibleWidth = 1;
            ChatWidgets.Button(bar, font, _shape, "Cancel", 62, 25, () =>
            {
                SetReply(null);
                FocusComposer();
            }, out _);
            _replyBar = bar.gameObject;
            _replyBar.SetActive(false);
        }

        void SetReply(ChatMetadata reply)
        {
            _reply = reply;
            if (_replyBar == null) return;
            _replyBar.SetActive(reply != null);
            _composerSize.preferredHeight = ComposerHeight + (reply != null ? ReplyHeight : 0);
            if (reply != null)
                EmojiCatalog.SetText(_replyCaption, $"Replying to {reply.author}  ·  {reply.excerpt}");
        }

        void PickComposerEmoji()
        {
            int anchor = composer.selectionStringAnchorPosition;
            int focus = composer.selectionStringFocusPosition;
            ShowEmojiPicker(emoji =>
            {
                var draft = composer.text;
                int start = Mathf.Clamp(Mathf.Min(anchor, focus), 0, draft.Length);
                int end = Mathf.Clamp(Mathf.Max(anchor, focus), start, draft.Length);
                composer.text = draft.Substring(0, start) + emoji + draft.Substring(end);
                composer.stringPosition = start + emoji.Length;
                FocusComposer();
            });
        }

        void ShowEmojiPicker(Action<string> onChosen)
        {
            if (EmojiCatalog.Entries.Count == 0)
            {
                App.Report("The emoji catalog is unavailable.");
                return;
            }
            if (_emojiPicker == null)
                _emojiPicker = EmojiPicker.Create((RectTransform)transform, composer.textComponent.font, _shape);
            _emojiPicker.Show(onChosen);
        }

        // ---- Header and composer ----

        void OnPresence(string id)
        {
            if (id == _peerId) RefreshHeader();
        }

        void OnConnectionChanged()
        {
            RefreshHeader();
            // A failed connection loses channel membership and any messages sent meanwhile,
            // so the conversation is reloaded once we're connected again.
            if (App.Vivox.State == ConnectionState.Failed) _loaded = false;
            if (App.Vivox.State == ConnectionState.Connected && _key != null && !_loaded && !loading.activeSelf)
            {
                var key = _key;
                Clear();
                _key = key;
                LoadAsync(_version).Forget();
            }
        }

        void RefreshHeader()
        {
            if (_key == null) return;
            bool direct = _peerId != null;
            headerIcon.gameObject.SetActive(!direct);
            headerAvatar.gameObject.SetActive(direct);
            title.text = direct ? App.Friends.NameOf(_peerId) : _label.Substring(1);
            if (direct)
            {
                headerAvatar.Set(_peerId, title.text);
                headerAvatar.SetStatus(App.Friends.StatusOf(_peerId));
            }

            var activity = direct ? App.Friends.ActivityOf(_peerId) : null;
            topic.text = activity?.status ?? "";
            callButton.gameObject.SetActive(direct && App.Friends.IsFriend(_peerId));
            profileButton.gameObject.SetActive(direct);

            bool blocked = direct && App.Friends.IsBlocked(_peerId);
            bool online = App.Vivox.IsLoggedIn;
            composer.interactable = online && !blocked;
            _emojiButton.interactable = composer.interactable;
            if (!composer.interactable && _emojiPicker != null) _emojiPicker.Close();
            RefreshSendButton();
            placeholder.text = blocked ? "You can't message a user you have blocked."
                : online ? $"Message {_label}"
                : "Connecting to chat…";
        }

        void Send()
        {
            var text = composer.text.Trim();
            if (text.Length == 0 || _key == null || !composer.interactable) return;
            var reply = _reply;
            composer.text = "";
            SetReply(null);
            FocusComposer();
            SendAsync(_key, text, reply, _version).Forget();
        }

        void RefreshSendButton()
        {
            _sendButton.interactable = composer.interactable && _key != null &&
                !string.IsNullOrWhiteSpace(composer.text);
        }

        async UniTaskVoid SendAsync(string key, string text, ChatMetadata reply, int version)
        {
            // The message appears once Vivox delivers it back to us; nothing is added locally.
            try
            {
                await App.Chat.SendReplyAsync(key, text, reply);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                App.Report(e is VivoxApiException { StatusCode: 5103 }
                    ? "Message is too long. Shorten it and try again."
                    : "Message not sent. Try again.");
                if (version == _version && composer.text.Length == 0 && _reply == null)
                {
                    composer.text = text;
                    SetReply(reply);
                }
            }
        }

        void FocusComposer()
        {
            if (!composer.interactable || !composer.gameObject.activeInHierarchy) return;
            composer.Select();
            composer.ActivateInputField();
        }
    }
}
