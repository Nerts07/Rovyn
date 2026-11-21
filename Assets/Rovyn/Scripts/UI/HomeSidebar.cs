using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Secondary sidebar in the home view: search, Friends link and the DM list.
    public class HomeSidebar : MonoBehaviour
    {
        public TMP_InputField search;
        public Button friendsButton;
        public HoverHighlight friendsHighlight;
        public GameObject pendingBadge;
        public TMP_Text pendingText;
        public Button newMessageButton;
        public DmEntry dmPrefab;
        public Transform dmContainer;
        public GameObject emptyState;

        ItemList<DmEntry> _list;
        string _selected; // player ID of the open DM, null when Friends is open

        void Awake()
        {
            _list = new ItemList<DmEntry>(dmPrefab, dmContainer);
            var hint = (TMP_Text)search.placeholder;
            hint.text = "Find a conversation";
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            search.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;
            var searchWrap = search.transform.parent;
            searchWrap.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
            searchWrap.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().padding = new RectOffset(12, 12, 11, 11);
            UiPresentation.AddSearchClear(search);
            friendsButton.onClick.AddListener(() => MainScreen.Instance.ShowFriends());
            newMessageButton.onClick.AddListener(() => MainScreen.Instance.friendsPage.ShowTab(FriendsTab.All, true));
            search.onValueChanged.AddListener(_ => Refresh());
            search.onSubmit.AddListener(_ => OpenFirstMatch());
        }

        void OnEnable()
        {
            App.Chat.DirectListChanged += Refresh;
            App.Chat.UnreadChanged += Refresh;
            App.Friends.Changed += Refresh;
            App.Friends.PresenceChanged += OnPresence;
            Refresh();
        }

        void OnDisable()
        {
            App.Chat.DirectListChanged -= Refresh;
            App.Chat.UnreadChanged -= Refresh;
            App.Friends.Changed -= Refresh;
            App.Friends.PresenceChanged -= OnPresence;
        }

        public void SetSelection(string playerId)
        {
            _selected = playerId;
            friendsHighlight.Selected = playerId == null && MainScreen.Instance.friendsPage.gameObject.activeSelf;
            Refresh();
        }

        void OnPresence(string _) => Refresh();

        // With a search term, friends without a conversation yet show up too,
        // so the box doubles as "find or start a conversation".
        List<string> Entries()
        {
            var query = search.text.Trim();
            var ids = App.Chat.DirectPartners.ToList();
            if (query.Length == 0) return ids;

            foreach (var friend in App.Friends.Friends)
                if (!ids.Contains(friend.Member.Id)) ids.Add(friend.Member.Id);
            return ids.Where(id => App.Friends.NameOf(id).IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        void Refresh()
        {
            if (_list == null || !isActiveAndEnabled) return; // OnEnable refreshes when shown
            var entries = Entries();
            _list.Bind(entries, (entry, id) => entry.Bind(id, id == _selected));
            emptyState.SetActive(entries.Count == 0);

            int pending = App.Friends.Incoming.Count;
            pendingBadge.SetActive(pending > 0);
            pendingText.text = pending.ToString();
        }

        void OpenFirstMatch()
        {
            var first = Entries().FirstOrDefault();
            if (first == null) return;
            search.text = "";
            MainScreen.Instance.OpenDirect(first);
        }
    }
}
