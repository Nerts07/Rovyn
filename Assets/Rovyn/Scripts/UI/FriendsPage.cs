using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    public enum FriendsTab { Online, All, Pending, Blocked, AddFriend }

    // Friends page: tabs, searchable list, and the Add Friend form.
    public class FriendsPage : MonoBehaviour
    {
        [Header("Tabs (Online, All, Pending, Blocked)")]
        public Button[] tabButtons;
        public HoverHighlight[] tabHighlights;
        public Button addFriendTab;
        public Image addFriendTabBackground;
        public TMP_Text addFriendTabLabel;
        public GameObject pendingBadge;
        public TMP_Text pendingText;

        [Header("List")]
        public GameObject listView;
        public TMP_InputField search;
        public TMP_Text countHeader;
        public FriendRow rowPrefab;
        public Transform rowContainer;
        public GameObject emptyState;
        public TMP_Text emptyText;

        [Header("Add Friend")]
        public GameObject addFriendView;
        public TMP_InputField addInput;
        public BusyButton addSubmit;
        public TMP_Text addResult;
        public TMP_Text myName;
        public Button copyMyName;
        public Button copyMyId;

        FriendsTab _tab = FriendsTab.Online;
        ItemList<FriendRow> _rows;
        UnityEngine.UI.HorizontalLayoutGroup _tabs;
        GameObject _tabIcon;
        GameObject _tabTitle;
        GameObject _tabDivider;
        bool _tabLayoutPending;
        bool? _compactTabs;

        void Awake()
        {
            _rows = new ItemList<FriendRow>(rowPrefab, rowContainer);
            _tabs = addFriendTab.transform.parent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            _tabIcon = _tabs.transform.Find("Icon").gameObject;
            _tabTitle = _tabs.transform.Find("Title").gameObject;
            _tabDivider = _tabs.transform.Find("Dot").gameObject;
            _tabs.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56;
            foreach (var button in tabButtons)
                button.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0;
            var activity = GetComponentInChildren<ResponsiveHide>(true);
            activity.minWidth = 820f;
            var activityLayout = activity.target.GetComponent<UnityEngine.UI.LayoutElement>();
            activityLayout.preferredWidth = 268f;
            activityLayout.flexibleWidth = 0;
            activity.target.GetComponent<UnityEngine.UI.Image>().color = Theme.Sidebar;

            UiPresentation.AddSearchClear(search);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                var tab = (FriendsTab)i;
                tabButtons[i].onClick.AddListener(() => ShowTab(tab, false));
            }
            addFriendTab.onClick.AddListener(() => ShowTab(FriendsTab.AddFriend, false));
            search.onValueChanged.AddListener(_ => Refresh());
            addInput.onValueChanged.AddListener(_ => addSubmit.button.interactable = addInput.text.Trim().Length > 0);
            addInput.onSubmit.AddListener(_ => SendRequest());
            addSubmit.button.onClick.AddListener(SendRequest);
            copyMyName.onClick.AddListener(() => Copy(App.Auth.FullName, "Name copied."));
            copyMyId.onClick.AddListener(() => Copy(App.Auth.PlayerId, "Player ID copied."));
        }

        void OnRectTransformDimensionsChange() => _tabLayoutPending = true;

        void LateUpdate()
        {
            if (!_tabLayoutPending) return;
            _tabLayoutPending = false;
            bool compact = ((RectTransform)transform).rect.width < 600f;
            if (_compactTabs == compact) return;
            _compactTabs = compact;
            _tabIcon.SetActive(!compact);
            _tabTitle.SetActive(!compact);
            _tabDivider.SetActive(!compact);
            _tabs.spacing = compact ? 4f : 8f;
            _tabs.padding = compact ? new RectOffset(8, 8, 0, 0) : new RectOffset(16, 16, 0, 0);
        }

        void OnEnable()
        {
            _tabLayoutPending = true;
            App.Friends.Changed += Refresh;
            App.Friends.PresenceChanged += OnPresence;
            Refresh();
        }

        void OnDisable()
        {
            App.Friends.Changed -= Refresh;
            App.Friends.PresenceChanged -= OnPresence;
        }

        public void ShowTab(FriendsTab tab, bool navigate)
        {
            if (navigate) MainScreen.Instance.ShowFriends();
            _tab = tab;
            if (tab == FriendsTab.AddFriend)
            {
                addResult.text = "";
                addInput.text = "";
                addSubmit.button.interactable = false;
                addInput.Select();
                addInput.ActivateInputField();
            }
            Refresh();
        }

        void OnPresence(string _) => Refresh();

        void Refresh()
        {
            if (_rows == null || !isActiveAndEnabled) return;

            for (int i = 0; i < tabHighlights.Length; i++)
                tabHighlights[i].Selected = (int)_tab == i;
            bool adding = _tab == FriendsTab.AddFriend;
            addFriendTabBackground.color = adding ? Theme.Clear : Theme.Accent;
            addFriendTabLabel.color = adding ? Theme.TextLink : Color.white;

            int incoming = App.Friends.Incoming.Count;
            pendingBadge.SetActive(incoming > 0);
            pendingText.text = incoming.ToString();

            listView.SetActive(!adding);
            addFriendView.SetActive(adding);
            if (adding)
            {
                myName.text = $"Your name is <b>{App.Auth.FullName}</b>";
                return;
            }

            var query = search.text.Trim();
            var rows = Rows()
                .Where(r => query.Length == 0 || NameOf(r.relationship).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(r => r.kind == FriendRowKind.Incoming ? 0 : 1)
                .ThenBy(r => NameOf(r.relationship), StringComparer.OrdinalIgnoreCase)
                .ToList();

            _rows.Bind(rows, (row, data) => row.Bind(data.relationship, data.kind));
            countHeader.text = $"{TabTitle(_tab)} – {rows.Count}";
            emptyState.SetActive(rows.Count == 0);
            emptyText.text = query.Length > 0 ? "No one matches that search." : EmptyMessage(_tab);
        }

        IEnumerable<(Relationship relationship, FriendRowKind kind)> Rows()
        {
            switch (_tab)
            {
                case FriendsTab.Online:
                    return App.Friends.Friends
                        .Where(r => FriendsStore.StatusOf(r.Member) != UserStatus.Offline)
                        .Select(r => (r, FriendRowKind.Friend));
                case FriendsTab.All:
                    return App.Friends.Friends.Select(r => (r, FriendRowKind.Friend));
                case FriendsTab.Pending:
                    return App.Friends.Incoming.Select(r => (r, FriendRowKind.Incoming))
                        .Concat(App.Friends.Outgoing.Select(r => (r, FriendRowKind.Outgoing)));
                case FriendsTab.Blocked:
                    return App.Friends.Blocked.Select(r => (r, FriendRowKind.Blocked));
                default:
                    return Enumerable.Empty<(Relationship, FriendRowKind)>();
            }
        }

        static string NameOf(Relationship r) => Names.Display(r.Member.Profile?.Name) is { Length: > 0 } n ? n : r.Member.Id;

        static string TabTitle(FriendsTab tab) => tab switch
        {
            FriendsTab.Online => "Online",
            FriendsTab.All => "All friends",
            FriendsTab.Pending => "Pending",
            _ => "Blocked",
        };

        static string EmptyMessage(FriendsTab tab) => tab switch
        {
            FriendsTab.Online => "None of your friends are online right now.",
            FriendsTab.All => "No friends yet. Use Add Friend to send a request.",
            FriendsTab.Pending => "There are no pending friend requests.",
            _ => "You haven't blocked anyone.",
        };

        void SendRequest() => SendRequestAsync().Forget();

        async UniTaskVoid SendRequestAsync()
        {
            var target = addInput.text.Trim();
            if (target.Length == 0) return;
            addSubmit.Busy = true;
            try
            {
                await App.Friends.SendRequestAsync(target);
                addResult.color = Theme.Green;
                addResult.text = App.Friends.IsFriend(LastAddedId(target))
                    ? $"You and {target} are now friends!"
                    : $"Success! Your friend request to <b>{target}</b> was sent.";
                addInput.text = "";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                addResult.color = Theme.ErrorText;
                addResult.text = FriendsStore.Describe(e);
            }
            finally
            {
                addSubmit.Busy = false;
                addSubmit.button.interactable = addInput.text.Trim().Length > 0;
            }
        }

        // Adding someone who already sent us a request makes us friends right away.
        static string LastAddedId(string target) =>
            App.Friends.Friends.FirstOrDefault(r => r.Member.Id == target || r.Member.Profile?.Name == target)?.Member.Id;

        static void Copy(string text, string notice)
        {
            GUIUtility.systemCopyBuffer = text;
            App.Report(notice);
        }
    }
}
