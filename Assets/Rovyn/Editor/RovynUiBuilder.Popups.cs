using Rovyn.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Avatar = Rovyn.UI.Avatar;

namespace Rovyn.EditorTools
{
    public static partial class RovynUiBuilder
    {
        struct Shell
        {
            public RectTransform holder;
            public FadePanel fade;
            public Button backdrop;
            public RectTransform card;
        }

        // Holder (always active, no graphic) > Panel (inactive until shown, fades) > Backdrop + Card.
        // The component goes on the holder so its Awake and button wiring run with the main screen.
        static Shell PopupShell(Transform parent, string name, float width, Color backdropColor)
        {
            var holder = Kit.Rect(name, parent);
            Kit.Stretch(holder);

            var panel = Kit.Rect("Panel", holder);
            Kit.Stretch(panel);
            panel.gameObject.AddComponent<CanvasGroup>();
            var fade = panel.gameObject.AddComponent<FadePanel>();

            var backdrop = Kit.Box(panel, "Backdrop", backdropColor, 0, raycast: true);
            Kit.Stretch(backdrop.rectTransform);
            var backdropButton = Kit.Btn(backdrop.gameObject, backdrop);

            // The card catches clicks itself, so only the dimmed area around it closes the popup.
            var card = Kit.Box(panel, "Card", Theme.Popup, 8, raycast: true);
            Kit.Center(card.rectTransform, width, 0);
            Kit.Fit(card.gameObject);
            fade.content = card.rectTransform;
            panel.gameObject.SetActive(false);

            return new Shell { holder = holder, fade = fade, backdrop = backdropButton, card = card.rectTransform };
        }

        // ---- Profile ----

        static ProfilePopup BuildProfilePopup(Transform parent)
        {
            var shell = PopupShell(parent, "Profile Popup", 440, Theme.Backdrop);
            var card = shell.card;
            Kit.VBox(card.gameObject, 0, Kit.Pad(8));

            var banner = Kit.Box(card, "Banner", Theme.Accent, 6);
            Kit.Elem(banner.gameObject, h: 100);
            var avatar = Kit.MakeAvatar(banner.transform, "Avatar", 88, status: true, statusRing: Theme.Popup);
            Kit.Place(avatar.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), 88, 88, 16, -44);

            // Top padding leaves room for the half of the avatar that hangs below the banner.
            var info = Column(card, "Info", 2, Kit.Pad(16, 16, 52, 8));
            var displayName = Kit.Label(info, "Display Name", "", 24, Theme.TextHeader, FontStyles.Bold);
            var fullName = Kit.Label(info, "Full Name", "", 14, Theme.TextMuted);
            var statusText = Kit.Label(info, "Status", "", 14, Theme.TextNormal);
            Kit.Divider(info).With(d => Kit.Elem(d.gameObject, h: 1));
            var idRow = Row(info, "Id Row", 4, Kit.Pad(0, 0, 6, 0));
            var playerId = Kit.Label(idRow, "Player Id", "", 12, Theme.TextMuted);
            Kit.Elem(playerId.gameObject, fw: 1);
            var copyId = Kit.IconButton(idRow, "Copy Id", "copy", 24, 16, Theme.Clear, Theme.Interactive);

            // Your own profile: status picker and custom status.
            var self = Column(card, "Self Section", 8, Kit.Pad(16, 16, 4, 12));
            Kit.Header(self, "STATUS");
            var statusRow = Row(self, "Status Row", 6, null, TextAnchor.MiddleLeft, true);
            var statusNames = new[] { "Online", "Idle", "Do Not Disturb", "Invisible" };
            var statusButtons = new Button[4];
            var statusHighlights = new HoverHighlight[4];
            for (int i = 0; i < statusNames.Length; i++)
                statusButtons[i] = NavRow(statusRow, statusNames[i] + " Status", statusNames[i], 32, out statusHighlights[i], size: 13);
            Kit.Header(self, "CUSTOM STATUS");
            var customStatus = Kit.InputBox(self, "Custom Status", "What's happening?", false, 36, Theme.Rail, 14);
            var selfButtons = Row(self, "Self Buttons", 8, null, TextAnchor.MiddleLeft, true);
            var save = Kit.TextButton(selfButtons, "Save Status", "Save", Theme.Accent, Color.white, 32, -1, 4, 13);
            var edit = Kit.TextButton(selfButtons, "Edit Profile", "Edit Profile", Theme.Composer, Color.white, 32, -1, 4, 13);

            // Someone else's profile: one row of actions, the code shows the ones that apply.
            var actions = Row(card, "Actions Section", 8, Kit.Pad(16, 16, 4, 16), TextAnchor.MiddleLeft, true);
            Button Action(string label, Color bg) =>
                Kit.TextButton(actions, label, label, bg, Color.white, 32, -1, 4, 12);
            var message = Action("Message", Theme.Accent);
            var call = Action("Call", Theme.GreenButton);
            var addFriend = Action("Add Friend", Theme.GreenButton);
            var accept = Action("Accept", Theme.GreenButton);
            var decline = Action("Ignore", Theme.Composer);
            var cancel = Action("Cancel Request", Theme.Composer);
            var remove = Action("Remove Friend", Theme.Composer);
            var block = Action("Block", Theme.DangerButton);
            var unblock = Action("Unblock", Theme.Composer);

            var view = shell.holder.gameObject.AddComponent<ProfilePopup>();
            view.panel = shell.fade;
            view.backdrop = shell.backdrop;
            view.banner = banner;
            view.avatar = avatar;
            view.displayName = displayName;
            view.fullName = fullName;
            view.statusText = statusText;
            view.playerIdText = playerId;
            view.copyIdButton = copyId;
            view.selfSection = self.gameObject;
            view.statusButtons = statusButtons;
            view.statusHighlights = statusHighlights;
            view.customStatus = customStatus;
            view.saveStatusButton = save;
            view.editProfileButton = edit;
            view.actionsSection = actions.gameObject;
            view.messageButton = message;
            view.callButton = call;
            view.addFriendButton = addFriend;
            view.acceptButton = accept;
            view.declineButton = decline;
            view.cancelButton = cancel;
            view.removeButton = remove;
            view.blockButton = block;
            view.unblockButton = unblock;
            return view;
        }

        // ---- Per-user voice menu ----

        static ParticipantMenu BuildParticipantMenu(Transform parent)
        {
            var shell = PopupShell(parent, "Participant Menu", 300, new Color(0f, 0f, 0f, 0.35f));
            var card = shell.card;
            Kit.VBox(card.gameObject, 12, Kit.Pad(16));

            var top = Row(card, "Top", 12);
            var avatar = Kit.MakeAvatar(top, "Avatar", 40);
            var name = Kit.Label(top, "Name", "", 16, Theme.TextHeader, FontStyles.Bold);
            Kit.Elem(name.gameObject, fw: 1);
            Kit.Divider(card);

            Kit.Header(card, "USER VOLUME");
            var volumeRow = Row(card, "Volume Row", 12);
            var volume = Kit.SliderBar(volumeRow, "Volume", 0, 200);
            Kit.Elem(volume.gameObject, fw: 1);
            volume.value = 100;
            var volumeValue = Kit.Label(volumeRow, "Value", "100%", 14, Theme.TextNormal, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            Kit.Elem(volumeValue.gameObject, w: 48);

            var muteRow = Row(card, "Mute Row", 12);
            var muteLabel = Kit.Label(muteRow, "Label", "Mute", 15, Theme.TextNormal);
            Kit.Elem(muteLabel.gameObject, fw: 1);
            var mute = Kit.Switch(muteRow, "Mute Toggle");
            var profile = Kit.TextButton(card, "Profile", "Profile", Theme.Composer, Color.white, 32, -1, 4, 13);

            var view = shell.holder.gameObject.AddComponent<ParticipantMenu>();
            view.panel = shell.fade;
            view.backdrop = shell.backdrop;
            view.avatar = avatar;
            view.nameText = name;
            view.volume = volume;
            view.volumeValue = volumeValue;
            view.muteToggle = mute;
            view.profileButton = profile;
            return view;
        }

        // ---- Incoming call ----

        static void BuildIncomingCall(Transform parent)
        {
            var holder = Kit.Rect("Incoming Call", parent);
            Kit.Stretch(holder);

            // The panel is the card itself, so it only blocks clicks where the card is.
            var panel = Kit.Box(holder, "Panel", Theme.Popup, 8, raycast: true);
            Kit.Place(panel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), 320, 0, 0, -24);
            Kit.VBox(panel.gameObject, 12, Kit.Pad(20), TextAnchor.UpperCenter);
            Kit.Fit(panel.gameObject);
            panel.gameObject.AddComponent<CanvasGroup>();
            var fade = panel.gameObject.AddComponent<FadePanel>();
            fade.content = panel.rectTransform;

            var avatarRow = Row(panel.transform, "Avatar Row", 0, null, TextAnchor.MiddleCenter);
            var avatar = Kit.MakeAvatar(avatarRow, "Avatar", 64);
            var name = Kit.Label(panel.transform, "Name", "", 18, Theme.TextHeader, FontStyles.Bold, TextAlignmentOptions.Center);
            Kit.Label(panel.transform, "Text", "Incoming call…", 14, Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
            var buttons = Row(panel.transform, "Buttons", 24, Kit.Pad(0, 0, 4, 0), TextAnchor.MiddleCenter);
            var accept = Kit.IconButton(buttons, "Accept", "phone", 48, 24, Theme.GreenButton, Color.white);
            var decline = Kit.IconButton(buttons, "Decline", "phone_hangup", 48, 24, Theme.DangerButton, Color.white);
            panel.gameObject.SetActive(false);

            var view = holder.gameObject.AddComponent<IncomingCallPopup>();
            view.panel = fade;
            view.avatar = avatar;
            view.nameText = name;
            view.acceptButton = accept;
            view.declineButton = decline;
        }

        // ---- Tooltip ----

        static void BuildTooltip(Transform parent)
        {
            var holder = Kit.Rect("Tooltip", parent);
            Kit.Stretch(holder);

            // Pivot on the left edge, so the box sits to the right of the element it points at.
            var box = Kit.Box(holder, "Box", Theme.Popup, 4);
            Kit.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), 0, 0);
            Kit.HBox(box.gameObject, 0, Kit.Pad(10, 10, 6, 6), TextAnchor.MiddleCenter);
            Kit.Fit(box.gameObject, horizontal: true);
            var label = Kit.Label(box.transform, "Label", "", 14, Theme.TextHeader, FontStyles.Bold);
            box.gameObject.SetActive(false);

            var view = holder.gameObject.AddComponent<Tooltip>();
            view.box = box.rectTransform;
            view.label = label;
        }

        // ---- Settings ----

        static SettingsScreen BuildSettings(Transform parent)
        {
            var holder = Kit.Rect("Settings", parent);
            Kit.Stretch(holder);

            var panel = Kit.Box(holder, "Panel", Theme.Main, 0, raycast: true);
            Kit.Stretch(panel.rectTransform);
            Kit.HBox(panel.gameObject, 0, null, TextAnchor.UpperLeft, true, true, false, true);
            panel.gameObject.AddComponent<CanvasGroup>();
            var fade = panel.gameObject.AddComponent<FadePanel>();

            // Left menu
            var nav = Kit.Box(panel.transform, "Menu", Theme.Sidebar);
            Kit.Elem(nav.gameObject, w: 280);
            Kit.VBox(nav.gameObject, 2, Kit.Pad(20, 12, 60, 20));
            Kit.Header(nav.transform, "USER SETTINGS");
            var pageNames = new[] { "My Account", "Voice & Audio", "Appearance" };
            var pageButtons = new Button[3];
            var pageHighlights = new HoverHighlight[3];
            for (int i = 0; i < pageNames.Length; i++)
                pageButtons[i] = NavRow(nav.transform, pageNames[i] + " Tab", pageNames[i], 34, out pageHighlights[i]);
            Kit.Divider(nav.transform);
            var logout = NavRow(nav.transform, "Log Out", "Log Out", 34, out _, Theme.ErrorText);

            // Right side: three pages stacked, only one active at a time
            var content = Kit.Rect("Content", panel.transform);
            Kit.Elem(content.gameObject, fw: 1);
            var account = BuildAccountPage(content, out var accountRefs);
            var voice = BuildVoicePageSettings(content, out var voiceRefs);
            var appearance = BuildAppearancePage(content, out var zoom, out var zoomValue);
            voice.gameObject.SetActive(false);
            appearance.gameObject.SetActive(false);

            var close = Kit.IconButton(content, "Close", "close", 36, 20, Theme.Composer, Theme.Interactive);
            Kit.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), 36, 36, -40, -40);
            panel.gameObject.SetActive(false);

            var view = holder.gameObject.AddComponent<SettingsScreen>();
            view.panel = fade;
            view.closeButton = close;
            view.pageButtons = pageButtons;
            view.pageHighlights = pageHighlights;
            view.pages = new[] { account.gameObject, voice.gameObject, appearance.gameObject };
            view.logoutButton = logout;

            view.avatar = accountRefs.avatar;
            view.fullName = accountRefs.fullName;
            view.playerId = accountRefs.playerId;
            view.copyIdButton = accountRefs.copyId;
            view.displayName = accountRefs.displayName;
            view.saveName = accountRefs.saveName;
            view.currentPassword = accountRefs.currentPassword;
            view.newPassword = accountRefs.newPassword;
            view.savePassword = accountRefs.savePassword;

            view.inputDevice = voiceRefs.inputDevice;
            view.outputDevice = voiceRefs.outputDevice;
            view.inputVolume = voiceRefs.inputVolume;
            view.inputVolumeValue = voiceRefs.inputVolumeValue;
            view.outputVolume = voiceRefs.outputVolume;
            view.outputVolumeValue = voiceRefs.outputVolumeValue;
            view.micTest = voiceRefs.micTest;
            view.micTestIndicator = voiceRefs.micTestIndicator;
            view.voiceNotice = voiceRefs.notice;

            view.zoom = zoom;
            view.zoomValue = zoomValue;
            return view;
        }

        struct AccountRefs
        {
            public Avatar avatar;
            public TMP_Text fullName, playerId;
            public Button copyId;
            public FormField displayName, currentPassword, newPassword;
            public BusyButton saveName, savePassword;
        }

        struct VoiceRefs
        {
            public TMP_Dropdown inputDevice, outputDevice;
            public Slider inputVolume, outputVolume;
            public TMP_Text inputVolumeValue, outputVolumeValue, notice;
            public BusyButton micTest;
            public Image micTestIndicator;
        }

        // One settings page: a column with generous padding, stretched over the content area.
        static RectTransform SettingsPage(Transform parent, string name, string title)
        {
            var page = Column(parent, name, 20, Kit.Pad(60, 200, 60, 40));
            Kit.Stretch(page);
            var heading = Kit.Label(page, "Title", title, 20, Theme.TextHeader, FontStyles.Bold);
            Kit.Elem(heading.gameObject, h: 28);
            return page;
        }

        static RectTransform BuildAccountPage(Transform parent, out AccountRefs refs)
        {
            var page = SettingsPage(parent, "My Account", "My Account");
            refs = new AccountRefs();

            var card = Kit.Box(page, "Profile Card", Theme.Sidebar, 8);
            Kit.HBox(card.gameObject, 16, Kit.Pad(16));
            refs.avatar = Kit.MakeAvatar(card.transform, "Avatar", 80);
            var texts = Column(card.transform, "Texts", 4);
            Kit.Elem(texts.gameObject, fw: 1);
            refs.fullName = Kit.Label(texts, "Full Name", "", 20, Theme.TextHeader, FontStyles.Bold);
            var idRow = Row(texts, "Id Row", 4);
            refs.playerId = Kit.Label(idRow, "Player Id", "", 13, Theme.TextMuted);
            refs.copyId = Kit.IconButton(idRow, "Copy Id", "copy", 24, 16, Theme.Clear, Theme.Interactive);

            refs.displayName = Kit.Form(page, "Display Name", "DISPLAY NAME");
            var nameRow = Row(page, "Name Row", 0);
            refs.saveName = Kit.Busy(nameRow, "Save Name", "Save Changes", Theme.Accent, 36, 150);

            Kit.Divider(page).With(d => Kit.Elem(d.gameObject, h: 1));
            var passwordTitle = Kit.Label(page, "Password Title", "Change Password", 16, Theme.TextHeader, FontStyles.Bold);
            Kit.Elem(passwordTitle.gameObject, h: 22);
            refs.currentPassword = Kit.Form(page, "Current Password", "CURRENT PASSWORD", password: true);
            refs.newPassword = Kit.Form(page, "New Password", "NEW PASSWORD", password: true);
            var passwordRow = Row(page, "Password Row", 0);
            refs.savePassword = Kit.Busy(passwordRow, "Save Password", "Change Password", Theme.Accent, 36, 170);
            return page;
        }

        static RectTransform BuildVoicePageSettings(Transform parent, out VoiceRefs refs)
        {
            var page = SettingsPage(parent, "Voice & Audio", "Voice & Audio");
            refs = new VoiceRefs();

            refs.notice = Kit.Label(page, "Notice", "", 14, Theme.Yellow, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
            refs.notice.gameObject.SetActive(false);

            var devices = Row(page, "Devices", 16, null, TextAnchor.UpperLeft, true);
            var inputColumn = Column(devices, "Input Device", 8);
            Kit.Elem(inputColumn.gameObject, fw: 1);
            Kit.Header(inputColumn, "INPUT DEVICE");
            refs.inputDevice = Kit.Dropdown(inputColumn, "Input Dropdown");
            var outputColumn = Column(devices, "Output Device", 8);
            Kit.Elem(outputColumn.gameObject, fw: 1);
            Kit.Header(outputColumn, "OUTPUT DEVICE");
            refs.outputDevice = Kit.Dropdown(outputColumn, "Output Dropdown");

            var volumes = Row(page, "Volumes", 16, null, TextAnchor.UpperLeft, true);
            VolumeColumn(volumes, "Input Volume", "INPUT VOLUME", out refs.inputVolume, out refs.inputVolumeValue);
            VolumeColumn(volumes, "Output Volume", "OUTPUT VOLUME", out refs.outputVolume, out refs.outputVolumeValue);

            Kit.Divider(page).With(d => Kit.Elem(d.gameObject, h: 1));
            var testTitle = Kit.Label(page, "Mic Test Title", "Mic Test", 16, Theme.TextHeader, FontStyles.Bold);
            Kit.Elem(testTitle.gameObject, h: 22);
            Kit.Label(page, "Mic Test Hint", "Say something. The dot lights up while your microphone picks up sound.", 14,
                Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
            var testRow = Row(page, "Mic Test Row", 16);
            refs.micTest = Kit.Busy(testRow, "Mic Test", "Let's Check", Theme.Accent, 36, 150);
            refs.micTestIndicator = Kit.Img(testRow, "Indicator", Theme.Grey, Kit.Circle);
            Kit.Elem(refs.micTestIndicator.gameObject, w: 14, h: 14);
            return page;
        }

        static void VolumeColumn(Transform parent, string name, string title, out Slider slider, out TMP_Text value)
        {
            var column = Column(parent, name, 8);
            Kit.Elem(column.gameObject, fw: 1);
            Kit.Header(column, title);
            var row = Row(column, "Row", 12);
            slider = Kit.SliderBar(row, "Slider", 0, 200);
            Kit.Elem(slider.gameObject, fw: 1);
            slider.value = 100;
            value = Kit.Label(row, "Value", "100%", 14, Theme.TextNormal, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            Kit.Elem(value.gameObject, w: 52);
        }

        static RectTransform BuildAppearancePage(Transform parent, out Slider zoom, out TMP_Text zoomValue)
        {
            var page = SettingsPage(parent, "Appearance", "Appearance");
            Kit.Header(page, "ZOOM");
            var row = Row(page, "Zoom Row", 12);
            zoom = Kit.SliderBar(row, "Zoom", 50, 200);
            Kit.Elem(zoom.gameObject, fw: 1);
            zoom.value = 100;
            zoomValue = Kit.Label(row, "Value", "100%", 14, Theme.TextNormal, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            Kit.Elem(zoomValue.gameObject, w: 52);
            return page;
        }
    }
}
