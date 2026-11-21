using Rovyn.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Avatar = Rovyn.UI.Avatar;

namespace Rovyn.EditorTools
{
    public static partial class RovynUiBuilder
    {
        const float RailWidth = 72f;
        const float SidebarWidth = 240f;
        const float PanelHeight = 52f;
        const int PanelReserve = 68; // user panel height plus its 8 px margins, kept free under the rail and sidebar

        // ---- Small layout helpers ----

        // Vertical wrapper that only exists to add padding around one child.
        static RectTransform Wrap(Transform parent, string name, RectOffset pad)
        {
            var rt = Kit.Rect(name, parent);
            Kit.VBox(rt.gameObject, 0, pad);
            return rt;
        }

        static RectTransform Row(Transform parent, string name, float spacing = 8, RectOffset pad = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool expandW = false)
        {
            var rt = Kit.Rect(name, parent);
            Kit.HBox(rt.gameObject, spacing, pad, align, true, true, expandW, false);
            return rt;
        }

        static RectTransform Column(Transform parent, string name, float spacing = 8, RectOffset pad = null)
        {
            var rt = Kit.Rect(name, parent);
            Kit.VBox(rt.gameObject, spacing, pad);
            return rt;
        }

        static TextMeshProUGUI SectionHeader(Transform parent, string text, RectOffset pad)
        {
            var rt = Row(parent, text + " Row", 0, pad, TextAnchor.LowerLeft, true);
            Kit.Elem(rt.gameObject, h: 30);
            return Kit.Header(rt, text);
        }

        // Search field with a leading magnifier.
        static TMP_InputField SearchBox(Transform parent, string name, string placeholder, float height, float size)
        {
            var input = Kit.InputBox(parent, name, placeholder, false, height, Theme.Rail, size);
            ((RectTransform)input.textViewport).offsetMin = new Vector2(38, 0);
            var icon = Kit.Icon(input.transform, "Icon", "search", 18, Theme.TextMuted);
            Kit.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 18, 18, 12, 0);
            return input;
        }

        // Selectable list row used by the tab strip and the settings menu.
        static Button NavRow(Transform parent, string name, string text, float height, out HoverHighlight highlight,
            Color? labelColor = null, float size = 15, float width = -1)
        {
            var bg = Kit.Box(parent, name, Theme.Clear, 4, raycast: true);
            Kit.Elem(bg.gameObject, h: height, w: width);
            Kit.HBox(bg.gameObject, 6, Kit.Pad(10, 10, 0, 0), TextAnchor.MiddleLeft);
            var button = Kit.Btn(bg.gameObject, bg);
            var label = Kit.Label(bg.transform, "Label", text, size, labelColor ?? Theme.Interactive, FontStyles.Bold);
            Kit.Elem(label.gameObject, fw: 1);
            highlight = Kit.Hover(bg.gameObject, bg, new Graphic[] { label }, null, labelColor ?? Theme.Interactive,
                labelColor ?? Theme.InteractiveHover, labelColor ?? Color.white);
            return button;
        }

        // ---- Main screen ----

        static MainScreen BuildMainScreen(Transform parent, Prefabs p, ServerCatalog catalog)
        {
            var screen = Screen(parent, "Main Screen", Theme.Main);
            var main = screen.gameObject.AddComponent<MainScreen>();
            main.catalog = catalog;

            var body = Kit.Rect("Body", screen);
            Kit.Stretch(body);
            Kit.VBox(body.gameObject, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            // Connection banner is the first row of the body and pushes everything down while shown.
            var banner = Kit.Box(body, "Banner", Theme.Accent, 0);
            Kit.Elem(banner.gameObject, h: 32);
            Kit.HBox(banner.gameObject, 12, Kit.Pad(16, 16, 0, 0), TextAnchor.MiddleCenter);
            var bannerText = Kit.Label(banner.transform, "Message", "", 14, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Kit.Elem(bannerText.gameObject, fw: 1);
            var reconnect = Kit.TextButton(banner.transform, "Reconnect", "Reconnect", Color.white, Theme.Hex(0x1E1F22), 24, 100, 4, 13);
            reconnect.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            var bannerView = body.gameObject.AddComponent<ConnectionBanner>();
            bannerView.root = banner.gameObject;
            bannerView.background = banner;
            bannerView.message = bannerText;
            bannerView.reconnectButton = reconnect;

            var columns = Kit.Rect("Columns", body);
            Kit.Elem(columns.gameObject, fh: 1);
            Kit.HBox(columns.gameObject, 0, null, TextAnchor.UpperLeft, true, true, false, true);

            BuildRail(columns, p, main);
            var left = BuildLeftColumn(columns, p, main);

            var pages = Kit.Rect("Pages", columns);
            Kit.Elem(pages.gameObject, w: 0, minW: 0, fw: 1);
            main.friendsPage = BuildFriendsPage(pages, p);
            main.chatPage = BuildChatPage(pages, p);
            main.voicePage = BuildVoicePage(pages, p);
            main.chatPage.gameObject.SetActive(false);
            main.voicePage.gameObject.SetActive(false);
            left.SetActive(true);

            // Floats over the bottom of the rail and the sidebar, so it is built last among the columns.
            BuildUserPanel(columns);

            // Overlays are siblings of the body so they draw over everything. Order is back to front.
            main.settings = BuildSettings(screen);
            main.profile = BuildProfilePopup(screen);
            main.participantMenu = BuildParticipantMenu(screen);
            BuildIncomingCall(screen);
            BuildTooltip(screen);
            return main;
        }

        static void BuildRail(Transform parent, Prefabs p, MainScreen main)
        {
            var rail = Kit.Box(parent, "Server Rail", Theme.Rail);
            Kit.Elem(rail.gameObject, w: RailWidth, fw: 0);
            Kit.VBox(rail.gameObject, 8, Kit.Pad(0, 0, 12, PanelReserve), TextAnchor.UpperCenter, true, true, false, false);

            var home = MakeServerButton(rail.transform, "Home Button", true);
            home.tooltip = "Home";
            var separator = Kit.Box(rail.transform, "Separator", Theme.Divider, 1);
            Kit.Elem(separator.gameObject, w: 32, h: 2);
            var container = Kit.Rect("Servers", rail.transform);
            Kit.VBox(container.gameObject, 8, null, TextAnchor.UpperCenter, true, true, false, false);

            var view = rail.gameObject.AddComponent<ServerRail>();
            view.homeButton = home;
            view.serverPrefab = p.serverButton;
            view.serverContainer = container;
            main.rail = view;
        }

        // Sidebars stacked in one area, then the voice strip and the user panel below them.
        static GameObject BuildLeftColumn(Transform parent, Prefabs p, MainScreen main)
        {
            var left = Kit.Box(parent, "Left Column", Theme.Sidebar);
            Kit.Elem(left.gameObject, w: SidebarWidth, fw: 0);
            Kit.VBox(left.gameObject, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            var area = Kit.Rect("Sidebar Area", left.transform);
            Kit.Elem(area.gameObject, fh: 1);
            main.homeSidebar = BuildHomeSidebar(area, p);
            main.serverSidebar = BuildServerSidebar(area, p);
            main.serverSidebar.gameObject.SetActive(false);

            // Voice strip
            var strip = Kit.Box(left.transform, "Voice Strip", Theme.UserPanel);
            Kit.Elem(strip.gameObject, h: 52);
            Kit.HBox(strip.gameObject, 8, Kit.Pad(12, 8, 0, 0));
            var texts = Kit.Rect("Texts", strip.transform);
            Kit.VBox(texts.gameObject, 0, null, TextAnchor.MiddleLeft);
            Kit.Elem(texts.gameObject, fw: 1);
            var state = Kit.Label(texts, "State", "Voice Connected", 13, Theme.Green, FontStyles.Bold);
            var channel = Kit.Label(texts, "Channel", "", 12, Theme.TextMuted);
            var open = Kit.IconButton(strip.transform, "Open Voice", "speaker", 32, 20, Theme.Clear, Theme.Interactive);
            Kit.Hover(open.gameObject, open.targetGraphic, new[] { open.transform.Find("Icon").GetComponent<Graphic>() });
            var disconnect = Kit.IconButton(strip.transform, "Disconnect", "phone_hangup", 32, 20, Theme.Clear, Theme.Interactive);
            Kit.Hover(disconnect.gameObject, disconnect.targetGraphic, new[] { disconnect.transform.Find("Icon").GetComponent<Graphic>() });
            strip.gameObject.SetActive(false);

            var bar = left.gameObject.AddComponent<VoiceStatusBar>();
            bar.root = strip.gameObject;
            bar.stateText = state;
            bar.channelText = channel;
            bar.openButton = open;
            bar.disconnectButton = disconnect;

            var spacer = Kit.Rect("Panel Spacer", left.transform);
            Kit.Elem(spacer.gameObject, h: PanelReserve);
            return left.gameObject;
        }

        // The main screen positions the user panel below the sidebar.
        static void BuildUserPanel(Transform parent)
        {
            var panel = Kit.Box(parent, "User Panel", Theme.UserPanel, 8);
            Kit.Elem(panel.gameObject, ignore: true);
            Kit.Place(panel.rectTransform, Vector2.zero, Vector2.zero, RailWidth + SidebarWidth - 16f, PanelHeight, 8, 8);
            Kit.HBox(panel.gameObject, 2, Kit.Pad(8, 8, 0, 0));

            var profile = Kit.Box(panel.transform, "Profile", Theme.Clear, 4, raycast: true);
            Kit.Elem(profile.gameObject, fw: 1, h: 40);
            Kit.HBox(profile.gameObject, 8, Kit.Pad(4, 4, 0, 0));
            var profileButton = Kit.Btn(profile.gameObject, profile);
            Kit.Hover(profile.gameObject, profile);
            var avatar = Kit.MakeAvatar(profile.transform, "Avatar", 32, status: true, statusRing: Theme.UserPanel);
            var texts = Kit.Rect("Texts", profile.transform);
            Kit.VBox(texts.gameObject, 0, null, TextAnchor.MiddleLeft);
            Kit.Elem(texts.gameObject, fw: 1);
            var name = Kit.Label(texts, "Name", "", 14, Theme.TextHeader, FontStyles.Bold);
            var status = Kit.Label(texts, "Status", "", 12, Theme.TextMuted);

            var mic = PanelIcon(panel.transform, "Mic", "mic");
            var deafen = PanelIcon(panel.transform, "Deafen", "headphones");
            var settings = PanelIcon(panel.transform, "Settings", "gear");

            var view = panel.gameObject.AddComponent<UserPanel>();
            view.profileButton = profileButton;
            view.avatar = avatar;
            view.nameText = name;
            view.statusText = status;
            view.micButton = mic;
            view.micIcon = mic.transform.Find("Icon").GetComponent<Image>();
            view.micOn = Kit.Icon("mic");
            view.micOff = Kit.Icon("mic_off");
            view.deafenButton = deafen;
            view.deafenIcon = deafen.transform.Find("Icon").GetComponent<Image>();
            view.headphonesOn = Kit.Icon("headphones");
            view.headphonesOff = Kit.Icon("headphones_off");
            view.settingsButton = settings;
        }

        static Button PanelIcon(Transform parent, string name, string icon)
        {
            var button = Kit.IconButton(parent, name, icon, 32, 20, Theme.Clear, Theme.Interactive);
            Kit.Hover(button.gameObject, button.targetGraphic);
            return button;
        }

        // ---- Home sidebar ----

        static HomeSidebar BuildHomeSidebar(Transform parent, Prefabs p)
        {
            var root = Kit.Rect("Home Sidebar", parent);
            Kit.Stretch(root);
            Kit.VBox(root.gameObject, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            var searchWrap = Wrap(root, "Search Wrap", Kit.Pad(10, 10, 10, 10));
            Kit.Elem(searchWrap.gameObject, h: 48);
            var search = Kit.InputBox(searchWrap, "Search", "Find or start a conversation", false, 28, Theme.Rail, 13);
            var searchHint = (TMP_Text)search.placeholder;
            searchHint.alignment = TextAlignmentOptions.Center; // reads as a button until you type
            searchHint.color = Theme.Interactive;
            Kit.Divider(root);

            var friendsWrap = Wrap(root, "Friends Wrap", Kit.Pad(8, 8, 8, 0));
            var friendsRow = Kit.Box(friendsWrap, "Friends Button", Theme.Clear, 4, raycast: true);
            Kit.Elem(friendsRow.gameObject, h: 42);
            Kit.HBox(friendsRow.gameObject, 12, Kit.Pad(12, 12, 0, 0));
            var friendsButton = Kit.Btn(friendsRow.gameObject, friendsRow);
            var friendsIcon = Kit.Icon(friendsRow.transform, "Icon", "friends", 24, Theme.Interactive);
            var friendsLabel = Kit.Label(friendsRow.transform, "Label", "Friends", 16, Theme.Interactive, FontStyles.Bold);
            Kit.Elem(friendsLabel.gameObject, fw: 1);
            var pendingBadge = Kit.Box(friendsRow.transform, "Pending Badge", Theme.DangerButton, 8);
            Kit.Elem(pendingBadge.gameObject, minW: 16, h: 16);
            Kit.HBox(pendingBadge.gameObject, 0, Kit.Pad(5, 5, 0, 0), TextAnchor.MiddleCenter);
            var pendingText = Kit.Label(pendingBadge.transform, "Count", "0", 11, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            pendingBadge.gameObject.SetActive(false);
            var friendsHighlight = Kit.Hover(friendsRow.gameObject, friendsRow, new Graphic[] { friendsIcon, friendsLabel },
                null, Theme.Interactive, Theme.InteractiveHover, Color.white);

            var header = Row(root, "DMs Row", 0, Kit.Pad(18, 8, 8, 0), TextAnchor.MiddleLeft, true);
            Kit.Elem(header.gameObject, h: 36);
            var headerText = Kit.Label(header, "Direct Messages Header", "Direct Messages", 13, Theme.TextMuted, FontStyles.Bold);
            Kit.Elem(headerText.gameObject, fw: 1);
            var newMessage = Kit.IconButton(header, "New Message", "plus", 24, 16, Theme.Clear, Theme.Interactive);
            Kit.Hover(newMessage.gameObject, newMessage.targetGraphic, new[] { newMessage.transform.Find("Icon").GetComponent<Graphic>() },
                null, Theme.Interactive, Theme.InteractiveHover, Color.white);

            var scroll = Kit.Scroll(root, "DM List", out var dmContent, 2, Kit.Pad(8, 8, 0, 8));
            Kit.Elem(scroll.gameObject, fh: 1);
            var empty = Kit.Label(scroll.transform, "Empty", "No conversations yet.", 14, Theme.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Top, wrap: true);
            Kit.Place(empty.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), 200, 40, 0, -16);

            var view = root.gameObject.AddComponent<HomeSidebar>();
            view.search = search;
            view.friendsButton = friendsButton;
            view.friendsHighlight = friendsHighlight;
            view.pendingBadge = pendingBadge.gameObject;
            view.pendingText = pendingText;
            view.newMessageButton = newMessage;
            view.dmPrefab = p.dmEntry;
            view.dmContainer = dmContent;
            view.emptyState = empty.gameObject;
            return view;
        }

        // ---- Server sidebar ----

        static ServerSidebar BuildServerSidebar(Transform parent, Prefabs p)
        {
            var root = Kit.Rect("Server Sidebar", parent);
            Kit.Stretch(root);
            Kit.VBox(root.gameObject, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            var header = Row(root, "Header", 0, Kit.Pad(16, 16, 0, 0), TextAnchor.MiddleLeft, true);
            Kit.Elem(header.gameObject, h: 48);
            var serverName = Kit.Label(header, "Server Name", "Server", 16, Theme.TextHeader, FontStyles.Bold);
            Kit.Elem(serverName.gameObject, fw: 1);
            Kit.Divider(root);

            Kit.Scroll(root, "Channels", out var content, 0, Kit.Pad(0, 0, 4, 8));
            Kit.Elem(content.parent.parent.gameObject, fh: 1);
            SectionHeader(content, "TEXT CHANNELS", Kit.Pad(16, 8, 12, 4));
            var text = Column(content, "Text Channels", 2);
            SectionHeader(content, "VOICE CHANNELS", Kit.Pad(16, 8, 12, 4));
            var voice = Column(content, "Voice Channels", 2);

            var view = root.gameObject.AddComponent<ServerSidebar>();
            view.serverName = serverName;
            view.channelPrefab = p.channelEntry;
            view.textContainer = text;
            view.voiceContainer = voice;
            view.textIcon = Kit.Icon("hash");
            view.voiceIcon = Kit.Icon("speaker");
            return view;
        }

        // ---- Friends page ----

        static FriendsPage BuildFriendsPage(Transform parent, Prefabs p)
        {
            var root = Kit.Rect("Friends Page", parent);
            Kit.Stretch(root);
            Kit.VBox(root.gameObject, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            // Top bar with the tabs
            var top = Row(root, "Top Bar", 8, Kit.Pad(16, 16, 0, 0));
            Kit.Elem(top.gameObject, h: 48);
            Kit.Icon(top, "Icon", "friends", 24, Theme.Grey);
            Kit.Label(top, "Title", "Friends", 16, Theme.TextHeader, FontStyles.Bold).With(l => Kit.Elem(l.gameObject, w: 60));
            Kit.Img(top, "Dot", Theme.Grey, Kit.Circle).With(i => Kit.Elem(i.gameObject, w: 4, h: 4));

            var tabNames = new[] { "Online", "All", "Pending", "Blocked" };
            var tabButtons = new Button[4];
            var tabHighlights = new HoverHighlight[4];
            GameObject pendingBadge = null;
            TMP_Text pendingText = null;
            for (int i = 0; i < tabNames.Length; i++)
            {
                tabButtons[i] = NavRow(top, tabNames[i] + " Tab", tabNames[i], 28, out tabHighlights[i], size: 15);
                if (i != 2) continue;

                var badge = Kit.Box(tabButtons[i].transform, "Pending Badge", Theme.DangerButton, 8);
                Kit.Elem(badge.gameObject, minW: 16, h: 16);
                Kit.HBox(badge.gameObject, 0, Kit.Pad(5, 5, 0, 0), TextAnchor.MiddleCenter);
                pendingText = Kit.Label(badge.transform, "Count", "0", 11, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                badge.gameObject.SetActive(false);
                pendingBadge = badge.gameObject;
            }
            var addTab = Kit.TextButton(top, "Add Friend Tab", "Add Friend", Theme.Accent, Color.white, 28, 96, 4, 14);
            var addTabBackground = (Image)addTab.targetGraphic;
            var addTabLabel = addTab.transform.Find("Label").GetComponent<TMP_Text>();
            Kit.Divider(root);

            // Body: list column plus the Active Now column
            var body = Kit.Rect("Body", root);
            Kit.Elem(body.gameObject, fh: 1);
            Kit.HBox(body.gameObject, 0, null, TextAnchor.UpperLeft, true, true, false, true);
            var responsive = body.gameObject.AddComponent<ResponsiveHide>();

            var listColumn = Kit.Rect("List Column", body);
            Kit.Elem(listColumn.gameObject, fw: 1);

            // List view
            var listView = Kit.Rect("List View", listColumn);
            Kit.Stretch(listView);
            Kit.VBox(listView.gameObject, 0, null, TextAnchor.UpperLeft, true, true, true, false);
            var searchWrap = Wrap(listView, "Search Wrap", Kit.Pad(20, 20, 16, 8));
            var search = SearchBox(searchWrap, "Search", "Search", 36, 14);
            var countWrap = Wrap(listView, "Count Wrap", Kit.Pad(20, 20, 18, 8));
            var count = Kit.Label(countWrap, "Count", "", 14, Theme.TextMuted, FontStyles.Bold);
            var scroll = Kit.Scroll(listView, "Rows", out var rows, 0, Kit.Pad(0, 0, 0, 8));
            Kit.Elem(scroll.gameObject, fh: 1);
            var empty = Kit.Label(scroll.transform, "Empty", "", 15, Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.Top, wrap: true);
            Kit.Place(empty.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), 320, 60, 0, -60);

            // Add friend view
            var addView = Kit.Rect("Add Friend View", listColumn);
            Kit.Stretch(addView);
            Kit.VBox(addView.gameObject, 12, Kit.Pad(30, 30, 24, 20));
            Kit.Label(addView, "Title", "Add Friend", 18, Theme.TextHeader, FontStyles.Bold).With(l => Kit.Elem(l.gameObject, h: 24));
            Kit.Label(addView, "Hint", "You can add friends with their name, like Name#1234, or with their player ID.", 14,
                Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
            var addRow = Row(addView, "Add Row", 12, null, TextAnchor.MiddleLeft, false);
            Kit.Elem(addRow.gameObject, h: 52);
            var addInput = Kit.InputBox(addRow, "Add Input", "Name#1234 or player ID", false, 52, Theme.Rail, 15);
            Kit.Elem(addInput.gameObject, fw: 1);
            var addSubmit = Kit.Busy(addRow, "Send Request", "Send Friend Request", Theme.Accent, 36, 200);
            var addResult = Kit.Label(addView, "Result", "", 14, Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
            Kit.Divider(addView);
            var myName = Kit.Label(addView, "My Name", "", 15, Theme.TextNormal, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
            var copyRow = Row(addView, "Copy Row", 8);
            var copyName = Kit.TextButton(copyRow, "Copy Name", "Copy name", Theme.Composer, Color.white, 32, 120, 4, 13);
            var copyId = Kit.TextButton(copyRow, "Copy Id", "Copy player ID", Theme.Composer, Color.white, 32, 140, 4, 13);
            addView.gameObject.SetActive(false);

            // Active Now column
            var active = Kit.Box(body, "Active Now", Theme.Main);
            Kit.Elem(active.gameObject, w: 340);
            Kit.VBox(active.gameObject, 12, Kit.Pad(16));
            var border = Kit.Box(active.transform, "Border", Theme.Divider);
            Kit.Elem(border.gameObject, ignore: true);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = new Vector2(0, 1);
            border.rectTransform.pivot = new Vector2(0, 0.5f);
            border.rectTransform.sizeDelta = new Vector2(1, 0);
            border.rectTransform.anchoredPosition = Vector2.zero;
            Kit.Label(active.transform, "Title", "Active Now", 20, Theme.TextHeader, FontStyles.Bold).With(l => Kit.Elem(l.gameObject, h: 28));
            var cards = Column(active.transform, "Cards", 12);
            var quiet = Column(active.transform, "Quiet", 4);
            Kit.Label(quiet, "Title", "It's quiet for now…", 16, Theme.TextHeader, FontStyles.Bold, TextAlignmentOptions.Center);
            Kit.Label(quiet, "Text", "When a friend starts voice chat, they show up here.", 14, Theme.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Top, true);
            var panelView = active.gameObject.AddComponent<ActiveNowPanel>();
            panelView.cardPrefab = p.activeNowCard;
            panelView.container = cards;
            panelView.emptyState = quiet.gameObject;
            responsive.target = active.gameObject;

            var page = root.gameObject.AddComponent<FriendsPage>();
            page.tabButtons = tabButtons;
            page.tabHighlights = tabHighlights;
            page.addFriendTab = addTab;
            page.addFriendTabBackground = addTabBackground;
            page.addFriendTabLabel = addTabLabel;
            page.pendingBadge = pendingBadge;
            page.pendingText = pendingText;
            page.listView = listView.gameObject;
            page.search = search;
            page.countHeader = count;
            page.rowPrefab = p.friendRow;
            page.rowContainer = rows;
            page.emptyState = empty.gameObject;
            page.emptyText = empty;
            page.addFriendView = addView.gameObject;
            page.addInput = addInput;
            page.addSubmit = addSubmit;
            page.addResult = addResult;
            page.myName = myName;
            page.copyMyName = copyName;
            page.copyMyId = copyId;
            return page;
        }

        // ---- Chat page ----

        static ChatPage BuildChatPage(Transform parent, Prefabs p)
        {
            var root = Kit.Rect("Chat Page", parent);
            Kit.Stretch(root);
            Kit.VBox(root.gameObject, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            var header = Row(root, "Header", 8, Kit.Pad(16, 16, 0, 0));
            Kit.Elem(header.gameObject, h: 48);
            var headerIcon = Kit.Icon(header, "Icon", "hash", 24, Theme.Grey);
            var headerAvatar = Kit.MakeAvatar(header, "Avatar", 24, status: true, statusRing: Theme.Main);
            var title = Kit.Label(header, "Title", "", 16, Theme.TextHeader, FontStyles.Bold);
            var topic = Kit.Label(header, "Topic", "", 14, Theme.TextMuted);
            Kit.Elem(topic.gameObject, fw: 1);
            var call = Kit.IconButton(header, "Call", "phone", 32, 22, Theme.Clear, Theme.Interactive);
            Kit.Hover(call.gameObject, call.targetGraphic, new[] { call.transform.Find("Icon").GetComponent<Graphic>() },
                null, Theme.Interactive, Theme.InteractiveHover, Color.white);
            var profile = Kit.IconButton(header, "Profile", "user", 32, 22, Theme.Clear, Theme.Interactive);
            Kit.Hover(profile.gameObject, profile.targetGraphic, new[] { profile.transform.Find("Icon").GetComponent<Graphic>() },
                null, Theme.Interactive, Theme.InteractiveHover, Color.white);
            Kit.Divider(root);

            var messages = Kit.Rect("Messages", root);
            Kit.Elem(messages.gameObject, fh: 1);
            var scroll = Kit.Scroll(messages, "Scroll", out var content, 0, Kit.Pad(0, 0, 0, 16));
            Kit.Stretch((RectTransform)scroll.transform);

            var welcome = Column(content, "Welcome", 4, Kit.Pad(16, 16, 24, 8));
            var avatarHolder = Row(welcome, "Avatar Holder", 0);
            var welcomeAvatar = Kit.MakeAvatar(avatarHolder, "Avatar", 80);
            var channelIcon = Kit.Img(avatarHolder, "Channel Icon", Theme.Composer, Kit.Circle);
            Kit.Elem(channelIcon.gameObject, w: 68, h: 68);
            var channelGlyph = Kit.Icon(channelIcon.transform, "Glyph", "hash", 36, Color.white);
            Kit.Center(channelGlyph.rectTransform, 36, 36);
            var welcomeTitle = Kit.Label(welcome, "Title", "", 28, Theme.TextHeader, FontStyles.Bold, TextAlignmentOptions.TopLeft, true);
            var welcomeText = Kit.Label(welcome, "Text", "", 15, Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
            welcome.gameObject.SetActive(false);

            var loading = Kit.Icon(messages, "Loading", "spinner", 32, Theme.Interactive);
            Kit.Center(loading.rectTransform, 32, 32);
            loading.gameObject.AddComponent<Spinner>();
            loading.gameObject.SetActive(false);

            var composerWrap = Kit.Rect("Composer Wrap", root);
            Kit.Elem(composerWrap.gameObject, h: 72);
            Kit.VBox(composerWrap.gameObject, 0, Kit.Pad(16, 16, 0, 24), TextAnchor.MiddleCenter);
            var composer = Kit.InputBox(composerWrap, "Composer", "Message", false, 48, Theme.Composer, 15);

            var page = root.gameObject.AddComponent<ChatPage>();
            page.headerIcon = headerIcon;
            page.headerAvatar = headerAvatar;
            page.title = title;
            page.topic = topic;
            page.callButton = call;
            page.profileButton = profile;
            page.scroll = scroll;
            page.content = content;
            page.messagePrefab = p.messageItem;
            page.welcome = welcome.gameObject;
            page.welcomeAvatar = welcomeAvatar;
            page.welcomeChannelIcon = channelIcon.gameObject;
            page.welcomeTitle = welcomeTitle;
            page.welcomeText = welcomeText;
            page.loading = loading.gameObject;
            page.composer = composer;
            page.placeholder = (TMP_Text)composer.placeholder;
            return page;
        }

        // ---- Voice page ----

        static VoicePage BuildVoicePage(Transform parent, Prefabs p)
        {
            var root = Kit.Rect("Voice Page", parent);
            Kit.Stretch(root);
            Kit.VBox(root.gameObject, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            var header = Row(root, "Header", 8, Kit.Pad(16, 16, 0, 0));
            Kit.Elem(header.gameObject, h: 48);
            Kit.Icon(header, "Icon", "speaker", 24, Theme.Grey);
            var title = Kit.Label(header, "Title", "Voice", 16, Theme.TextHeader, FontStyles.Bold);
            var subtitle = Kit.Label(header, "Subtitle", "", 14, Theme.TextMuted);
            Kit.Divider(root);

            var wrap = Kit.Rect("Grid Wrap", root);
            Kit.Elem(wrap.gameObject, fh: 1);
            var gridArea = Kit.Rect("Grid Area", wrap);
            Kit.Stretch(gridArea, 16, 16, 16, 16);
            var gridRect = Kit.Rect("Grid", gridArea);
            Kit.Stretch(gridRect);
            var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(12, 12);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.cellSize = new Vector2(320, 180);

            var tileAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/ParticipantTile.prefab");
            var tileObject = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(tileAsset, gridRect);
            tileObject.name = "Ringing Tile";
            tileObject.SetActive(false);
            var ringing = tileObject.GetComponent<ParticipantTile>();

            var empty = Kit.Label(wrap, "Empty", "You're not in a voice channel. Join one from a server, or call a friend.", 16,
                Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.Center, true);
            Kit.Center(empty.rectTransform, 420, 60);

            var controls = Row(root, "Controls", 16, null, TextAnchor.MiddleCenter, false);
            Kit.Elem(controls.gameObject, h: 88);
            var mute = Kit.IconButton(controls, "Mute", "mic", 52, 26, Theme.Composer, Color.white);
            var deafen = Kit.IconButton(controls, "Deafen", "headphones", 52, 26, Theme.Composer, Color.white);
            var settings = Kit.IconButton(controls, "Settings", "gear", 52, 26, Theme.Composer, Color.white);
            var leave = Kit.IconButton(controls, "Leave", "phone_hangup", 52, 26, Theme.DangerButton, Color.white);

            var page = root.gameObject.AddComponent<VoicePage>();
            page.title = title;
            page.subtitle = subtitle;
            page.gridArea = gridArea;
            page.grid = grid;
            page.tilePrefab = p.participantTile;
            page.ringingTile = ringing;
            page.emptyState = empty.gameObject;
            page.controls = controls.gameObject;
            page.muteButton = mute;
            page.muteIcon = mute.transform.Find("Icon").GetComponent<Image>();
            page.micOn = Kit.Icon("mic");
            page.micOff = Kit.Icon("mic_off");
            page.deafenButton = deafen;
            page.deafenIcon = deafen.transform.Find("Icon").GetComponent<Image>();
            page.headphonesOn = Kit.Icon("headphones");
            page.headphonesOff = Kit.Icon("headphones_off");
            page.settingsButton = settings;
            page.leaveButton = leave;
            return page;
        }
    }
}
