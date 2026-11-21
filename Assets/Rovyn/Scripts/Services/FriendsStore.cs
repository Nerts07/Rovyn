using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Cysharp.Threading.Tasks;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;
using UnityEngine;

namespace Rovyn
{
    public enum UserStatus { Online, Idle, DoNotDisturb, Invisible, Offline }

    // Custom presence payload shared with friends. Public fields so Newtonsoft serializes them.
    [Serializable]
    public class PresenceActivity
    {
        public string status;       // custom status text
        public string voice;        // voice channel label while connected, otherwise null
        public string voiceChannel; // joinable Vivox channel, null for private calls
    }

    // Friends, requests, blocks and presence through UGS Friends.
    // The SDK keeps the relationship lists up to date from its event stream; this class only
    // forwards change notifications and maps SDK types to what the UI needs.
    public sealed class FriendsStore
    {
        public event Action Changed;
        internal event Action FriendRequestReceived;
        public event Action<string> PresenceChanged;
        public event Action<IMessageReceivedEvent> MessageReceived;
        public event Action ConnectionChanged;

        public bool IsReady { get; private set; }
        public bool IsConnected { get; private set; } = true;
        public UserStatus MyStatus { get; private set; } = UserStatus.Online;
        public PresenceActivity MyActivity { get; private set; } = new();

        const string StatusPref = "rovyn.status";
        const string CustomStatusPref = "rovyn.customStatus";

        static IFriendsService Service => FriendsService.Instance;
        static readonly IReadOnlyList<Relationship> Empty = Array.Empty<Relationship>();

        public IReadOnlyList<Relationship> Friends => IsReady ? Service.Friends : Empty;
        public IReadOnlyList<Relationship> Incoming => IsReady ? Service.IncomingFriendRequests : Empty;
        public IReadOnlyList<Relationship> Outgoing => IsReady ? Service.OutgoingFriendRequests : Empty;
        public IReadOnlyList<Relationship> Blocked => IsReady ? Service.Blocks : Empty;

        public async UniTask StartAsync()
        {
            var options = new InitializeOptions()
                .WithEvents(true)
                .WithMemberProfile(true)
                .WithMemberPresence(true);
            await Service.InitializeAsync(options);

            Service.RelationshipAdded += OnRelationshipAdded;
            Service.RelationshipDeleted += OnRelationshipDeleted;
            Service.PresenceUpdated += OnPresenceUpdated;
            Service.MessageReceived += OnMessageReceived;
            Service.NotificationsConnectivityChanged += OnConnectivityChanged;
            IsReady = true;
            IsConnected = true;

            MyStatus = (UserStatus)PlayerPrefs.GetInt(StatusPref, (int)UserStatus.Online);
            if (MyStatus == UserStatus.Offline) MyStatus = UserStatus.Online;
            MyActivity = new PresenceActivity { status = PlayerPrefs.GetString(CustomStatusPref, "") };
            await PushPresenceAsync();
            Changed?.Invoke();
        }

        public async UniTask StopAsync()
        {
            if (!IsReady) return;
            Service.RelationshipAdded -= OnRelationshipAdded;
            Service.RelationshipDeleted -= OnRelationshipDeleted;
            Service.PresenceUpdated -= OnPresenceUpdated;
            Service.MessageReceived -= OnMessageReceived;
            Service.NotificationsConnectivityChanged -= OnConnectivityChanged;

            // Tell friends we went offline before the access token goes away.
            try { await Service.SetPresenceAvailabilityAsync(Availability.Offline); }
            catch (Exception e) { Debug.LogWarning($"Could not set offline presence: {e.Message}"); }

            IsReady = false;
            Changed?.Invoke();
        }

        // ---- Presence ----

        public async UniTask SetStatusAsync(UserStatus status, string customStatus)
        {
            MyStatus = status;
            MyActivity.status = customStatus?.Trim() ?? "";
            PlayerPrefs.SetInt(StatusPref, (int)status);
            PlayerPrefs.SetString(CustomStatusPref, MyActivity.status);
            await PushPresenceAsync();
        }

        public async UniTask SetVoiceActivityAsync(string voiceLabel, string voiceChannel)
        {
            if (MyActivity.voice == voiceLabel && MyActivity.voiceChannel == voiceChannel) return;
            MyActivity.voice = voiceLabel;
            MyActivity.voiceChannel = voiceChannel;
            try { await PushPresenceAsync(); }
            catch (Exception e) { Debug.LogWarning($"Presence update failed: {e.Message}"); }
        }

        async UniTask PushPresenceAsync()
        {
            if (!IsReady) return;
            var availability = MyStatus switch
            {
                UserStatus.Idle => Availability.Away,
                UserStatus.DoNotDisturb => Availability.Busy,
                UserStatus.Invisible => Availability.Invisible,
                _ => Availability.Online,
            };
            // Invisible users still share nothing else.
            var activity = MyStatus == UserStatus.Invisible ? new PresenceActivity() : MyActivity;
            await Service.SetPresenceAsync(availability, activity);
            PresenceChanged?.Invoke(App.Auth.PlayerId);
        }

        // ---- Relationship actions ----

        // Accepts a full player name ("Name#1234") or a raw player ID.
        public async UniTask SendRequestAsync(string nameOrId)
        {
            nameOrId = nameOrId.Trim();
            if (nameOrId.Contains('#'))
                await Service.AddFriendByNameAsync(nameOrId);
            else
                await Service.AddFriendAsync(nameOrId);
            Changed?.Invoke();
        }

        // Accepting is the same call as sending: it turns the incoming request into a friendship.
        public async UniTask AcceptAsync(string memberId) { await Service.AddFriendAsync(memberId); Changed?.Invoke(); }
        public async UniTask DeclineAsync(string memberId) { await Service.DeleteIncomingFriendRequestAsync(memberId); Changed?.Invoke(); }
        public async UniTask CancelRequestAsync(string memberId) { await Service.DeleteOutgoingFriendRequestAsync(memberId); Changed?.Invoke(); }
        public async UniTask RemoveFriendAsync(string memberId) { await Service.DeleteFriendAsync(memberId); Changed?.Invoke(); }

        public async UniTask BlockAsync(string memberId)
        {
            await Service.AddBlockAsync(memberId);
            await App.Vivox.BlockAsync(memberId, true);
            Changed?.Invoke();
        }

        public async UniTask UnblockAsync(string memberId)
        {
            await Service.DeleteBlockAsync(memberId);
            await App.Vivox.BlockAsync(memberId, false);
            Changed?.Invoke();
        }

        // Friends messaging only works between friends. Used for call signaling.
        public async UniTask SendMessageAsync<T>(string memberId, T message) where T : new()
        {
            await Service.MessageAsync(memberId, message);
        }

        // ---- Lookups ----

        public bool IsFriend(string id) => Friends.Any(r => r.Member.Id == id);
        public bool IsBlocked(string id) => Blocked.Any(r => r.Member.Id == id);
        public bool HasOutgoing(string id) => Outgoing.Any(r => r.Member.Id == id);
        public bool HasIncoming(string id) => Incoming.Any(r => r.Member.Id == id);

        public List<string> BlockedIds() => Blocked.Select(r => r.Member.Id).ToList();

        public Member FindMember(string id)
        {
            if (!IsReady || string.IsNullOrEmpty(id)) return null;
            return Service.Relationships.FirstOrDefault(r => r.Member?.Id == id)?.Member;
        }

        // Best display name we know for a player ID.
        public string NameOf(string id, string fallback = null)
        {
            if (id == App.Auth.PlayerId) return App.Auth.DisplayName;
            var name = FindMember(id)?.Profile?.Name;
            if (!string.IsNullOrEmpty(name)) return Names.Display(name);
            return string.IsNullOrEmpty(fallback) ? Names.FromId(id) : fallback;
        }

        public string FullNameOf(string id)
        {
            if (id == App.Auth.PlayerId) return App.Auth.FullName;
            return FindMember(id)?.Profile?.Name ?? "";
        }

        public UserStatus StatusOf(string id)
        {
            if (id == App.Auth.PlayerId) return MyStatus;
            return StatusOf(FindMember(id));
        }

        public static UserStatus StatusOf(Member member)
        {
            return member?.Presence?.Availability switch
            {
                Availability.Online => UserStatus.Online,
                Availability.Away => UserStatus.Idle,
                Availability.Busy => UserStatus.DoNotDisturb,
                _ => UserStatus.Offline, // Invisible, Offline and Unknown all look offline to others
            };
        }

        public PresenceActivity ActivityOf(string id)
        {
            if (id == App.Auth.PlayerId) return MyActivity;
            return ActivityOf(FindMember(id));
        }

        public static PresenceActivity ActivityOf(Member member)
        {
            if (member?.Presence == null || StatusOf(member) == UserStatus.Offline) return null;
            try { return member.Presence.GetActivity<PresenceActivity>(); }
            catch { return null; } // no activity set yet
        }

        // ---- SDK events ----

        void OnRelationshipAdded(IRelationshipAddedEvent e)
        {
            Changed?.Invoke();
            var relationship = e.Relationship;
            if (relationship.Type == RelationshipType.FriendRequest &&
                relationship.Member is { Role: MemberRole.Source } sender && !IsBlocked(sender.Id))
                FriendRequestReceived?.Invoke();
        }

        void OnRelationshipDeleted(IRelationshipDeletedEvent e) => Changed?.Invoke();
        void OnPresenceUpdated(IPresenceUpdatedEvent e) => PresenceChanged?.Invoke(e.ID);
        void OnMessageReceived(IMessageReceivedEvent e) => MessageReceived?.Invoke(e);

        void OnConnectivityChanged(INotificationsStateChangedEvent e)
        {
            bool connected = e.State == FriendsEventConnectionState.Subscribed;
            if (connected == IsConnected) return;
            IsConnected = connected;
            ConnectionChanged?.Invoke();
            // Events may have been missed while disconnected.
            if (connected) RefreshAsync().Forget();
        }

        public async UniTaskVoid RefreshAsync()
        {
            try
            {
                await Service.ForceRelationshipsRefreshAsync();
                Changed?.Invoke();
            }
            catch (Exception e) { Debug.LogWarning($"Friends refresh failed: {e.Message}"); }
        }

        public static string Describe(Exception e)
        {
            if (e is FriendsServiceException f)
            {
                switch (f.ErrorCode)
                {
                    case FriendsErrorCode.UnknownProfileName:
                    case FriendsErrorCode.InvalidCreateMemberID:
                    case FriendsErrorCode.InvalidCreateTarget:
                        return "Couldn't find that user. Check the name (Name#1234) or player ID.";
                    case FriendsErrorCode.UserTargetingSelf: return "You can't add yourself.";
                    case FriendsErrorCode.FriendshipAlreadyExists: return "You're already friends with that user.";
                    case FriendsErrorCode.RelationshipAlreadyExists: return "A request is already pending with that user.";
                    case FriendsErrorCode.ActionUnauthorizedWhenBlocked: return "You can't do that while one of you has blocked the other.";
                    case FriendsErrorCode.FriendLimitReached: return "You've reached the friend limit.";
                    case FriendsErrorCode.FriendRequestLimitReached: return "You've reached the pending request limit.";
                    case FriendsErrorCode.TargetsFriendLimitReached: return "That user has reached their friend limit.";
                    case FriendsErrorCode.BlockLimitReached: return "You've reached the block limit.";
                    case FriendsErrorCode.ProjectNotEnabled: return "Friends is not enabled for this project in the Unity Dashboard.";
                    case FriendsErrorCode.RateLimited: return "Slow down a little and try again.";
                    case FriendsErrorCode.NetworkError: return "Can't reach the Friends service.";
                }
                // The service answers 500 instead of 404 when a name lookup finds nobody.
                if (f.StatusCode == HttpStatusCode.InternalServerError || f.StatusCode == HttpStatusCode.NotFound)
                    return "Couldn't find that user. Check the name (Name#1234) or player ID.";
            }
            if (e is RelationshipNotFoundException) return "That relationship no longer exists.";
            return e.Message;
        }
    }
}
