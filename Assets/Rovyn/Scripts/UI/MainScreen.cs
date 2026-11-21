using System.Collections;
using UnityEngine;

namespace Rovyn.UI
{
    // Navigation for the signed-in app: which sidebar and which page are on screen.
    public class MainScreen : MonoBehaviour
    {
        public static MainScreen Instance { get; private set; }

        public ServerCatalog catalog;
        public ServerRail rail;
        public HomeSidebar homeSidebar;
        public ServerSidebar serverSidebar;
        public FriendsPage friendsPage;
        public ChatPage chatPage;
        public VoicePage voicePage;
        public ProfilePopup profile;
        public SettingsScreen settings;
        public ParticipantMenu participantMenu;

        public ServerCatalog.Server CurrentServer { get; private set; }

        string _homeDirect;                 // DM open in the home view, null means the friends page
        readonly System.Collections.Generic.Dictionary<string, string> _lastChannel = new();

        const float RailWidth = 64f;
        const int ShellGap = 8;
        const int SidebarInset = 8;
        UnityEngine.UI.LayoutElement _sidebarLayout;
        RectTransform _userPanel;
        Coroutine _pageFade;
        CanvasGroup _fadingPage;

        void Awake()
        {
            Instance = this;
            ConfigureLayout();
            if (GetComponent<CallRingtone>() == null) gameObject.AddComponent<CallRingtone>();
            if (GetComponent<NotificationSound>() == null) gameObject.AddComponent<NotificationSound>();
        }

        void OnDisable() => FinishPageFade();

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            rail.Build(catalog);
            _homeDirect = null;
            ShowHome();
        }

        public void Close()
        {
            profile.Close();
            settings.Close();
            participantMenu.Close();
            chatPage.Clear();
            App.Chat.SetOpen(null);
        }

        // ---- Home (friends + direct messages) ----

        public void ShowHome()
        {
            if (_homeDirect != null) OpenDirect(_homeDirect);
            else ShowFriends();
        }

        public void ShowFriends()
        {
            EnterHome();
            _homeDirect = null;
            ShowPage(friendsPage.gameObject);
            homeSidebar.SetSelection(null);
        }

        public void OpenDirect(string playerId)
        {
            EnterHome();
            _homeDirect = playerId;
            ShowPage(chatPage.gameObject);
            chatPage.OpenDirect(playerId);
            homeSidebar.SetSelection(playerId);
        }

        void EnterHome()
        {
            CurrentServer = null;
            rail.Select(-1);
            homeSidebar.gameObject.SetActive(true);
            serverSidebar.gameObject.SetActive(false);
        }

        // ---- Servers ----

        public void OpenServer(int index)
        {
            var server = catalog.servers[index];
            CurrentServer = server;
            rail.Select(index);
            homeSidebar.gameObject.SetActive(false);
            serverSidebar.gameObject.SetActive(true);
            serverSidebar.Show(server);

            if (server.textChannels.Length == 0) { ShowVoice(); return; }
            var channel = _lastChannel.TryGetValue(server.id, out var last) ? last : server.textChannels[0];
            OpenTextChannel(channel);
        }

        public void OpenTextChannel(string channel)
        {
            if (CurrentServer == null) return;
            _lastChannel[CurrentServer.id] = channel;
            ShowPage(chatPage.gameObject);
            chatPage.OpenChannel(CurrentServer, channel);
            serverSidebar.SetSelection(channel);
        }

        public void JoinVoiceChannel(string channel)
        {
            if (CurrentServer == null) return;
            var id = ServerCatalog.VoiceChannelId(CurrentServer, channel);
            ShowVoice();
            if (App.Voice.Channel == id) return;
            UiActions.Run(async () =>
            {
                if (App.Calls.State != CallState.None) await App.Calls.HangUpAsync();
                await App.Voice.JoinAsync(id, channel);
            });
        }

        // ---- Shared pages and popups ----

        public void ShowVoice()
        {
            ShowPage(voicePage.gameObject);
            serverSidebar.SetSelection(null);
            homeSidebar.SetSelection(null);
        }

        public void ShowProfile(string playerId, bool fromChat = false) => profile.Open(playerId, fromChat);
        public void ShowSettings() => settings.Open();

        public void StartCall(string playerId)
        {
            OpenDirect(playerId);
            ShowVoice();
            UiActions.Run(() => App.Calls.StartCallAsync(playerId));
        }

        void ShowPage(GameObject page)
        {
            bool changed = !page.activeSelf;
            friendsPage.gameObject.SetActive(page == friendsPage.gameObject);
            chatPage.gameObject.SetActive(page == chatPage.gameObject);
            voicePage.gameObject.SetActive(page == voicePage.gameObject);
            if (page != chatPage.gameObject) App.Chat.SetOpen(null);
            if (!changed) return;

            FinishPageFade();
            var group = page.GetComponent<CanvasGroup>();
            if (group == null) group = page.AddComponent<CanvasGroup>();
            if (group == null) return;
            _fadingPage = group;
            _pageFade = StartCoroutine(RevealPage(group));
        }

        IEnumerator RevealPage(CanvasGroup group)
        {
            const float duration = 0.16f;
            for (float elapsed = 0f; elapsed < duration && group != null; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.SmoothStep(0.45f, 1f, elapsed / duration);
                yield return null;
            }
            if (group != null) group.alpha = 1f;
            _pageFade = null;
            _fadingPage = null;
        }

        void FinishPageFade()
        {
            if (_pageFade != null) StopCoroutine(_pageFade);
            if (_fadingPage != null) _fadingPage.alpha = 1f;
            _pageFade = null;
            _fadingPage = null;
        }

        void ConfigureLayout()
        {
            var columns = rail.transform.parent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            columns.padding = new RectOffset(ShellGap, 12, 12, 12);
            columns.spacing = ShellGap;
            columns.childForceExpandWidth = false;

            LockWidth(rail.GetComponent<UnityEngine.UI.LayoutElement>(), RailWidth);
            var railLayout = rail.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            railLayout.padding = new RectOffset(0, 0, 12, 12);

            var sidebar = homeSidebar.transform.parent.parent;
            _sidebarLayout = sidebar.GetComponent<UnityEngine.UI.LayoutElement>();
            var sidebarImage = sidebar.GetComponent<UnityEngine.UI.Image>();
            sidebarImage.color = Theme.Sidebar;
            var userPanel = columns.GetComponentInChildren<UserPanel>(true);
            _userPanel = (RectTransform)userPanel.transform;
            var panelImage = userPanel.GetComponent<UnityEngine.UI.Image>();

            GetComponent<UnityEngine.UI.Image>().color = Theme.Rail;
            UiPresentation.RoundSurface(sidebarImage, panelImage.sprite, 12f);
            UiPresentation.RoundSurface(panelImage, panelImage.sprite, 8f);

            var pages = friendsPage.transform.parent;
            var pagesLayout = pages.GetComponent<UnityEngine.UI.LayoutElement>();
            pagesLayout.minWidth = 0;
            pagesLayout.preferredWidth = 0;
            pagesLayout.flexibleWidth = 1;
            var surface = pages.gameObject.AddComponent<UnityEngine.UI.Image>();
            surface.color = Theme.Main;
            surface.raycastTarget = false;
            UiPresentation.RoundSurface(surface, panelImage.sprite, 12f);

            OnRectTransformDimensionsChange();
        }

        void OnRectTransformDimensionsChange()
        {
            if (_sidebarLayout == null || _userPanel == null) return;
            float viewportWidth = ((RectTransform)transform).rect.width;
            float width = viewportWidth < 920f ? 184f : 244f;
            LockWidth(_sidebarLayout, width);
            _userPanel.anchoredPosition = new Vector2(ShellGap * 2 + RailWidth + SidebarInset, 12f + SidebarInset);
            _userPanel.sizeDelta = new Vector2(width - 2 * SidebarInset, 52f);
        }

        static void LockWidth(UnityEngine.UI.LayoutElement layout, float width)
        {
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0;
        }
    }
}
