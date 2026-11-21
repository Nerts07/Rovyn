using System.IO;
using Rovyn.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Avatar = Rovyn.UI.Avatar;

namespace Rovyn.EditorTools
{
    // One-time builder for the Rovyn scene and prefabs. Menu: Rovyn > Build UI (Scene + Prefabs).
    // Re-running overwrites the scene and the prefabs and keeps the server catalog.
    public static partial class RovynUiBuilder
    {
        const string Root = "Assets/Rovyn";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string DataDir = Root + "/Data";
        const string ScenePath = SceneDir + "/Rovyn.unity";
        const string CatalogPath = DataDir + "/ServerCatalog.asset";

        struct Prefabs
        {
            public ServerButton serverButton;
            public ChannelEntry channelEntry;
            public DmEntry dmEntry;
            public FriendRow friendRow;
            public MessageItem messageItem;
            public ParticipantTile participantTile;
            public ActiveNowCard activeNowCard;
        }

        [MenuItem("Rovyn/Build UI (Scene + Prefabs)")]
        public static void Build()
        {
            if (TMP_Settings.instance == null)
            {
                EditorUtility.DisplayDialog("Rovyn", "Import the TextMeshPro essentials first (Window > TextMeshPro > Import TMP Essential Resources), then run the builder again.", "OK");
                return;
            }
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild Rovyn UI",
                    "This overwrites Rovyn.unity and the prefabs in Assets/Rovyn/Prefabs. Your server catalog is kept.", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Kit.ClearCache();
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Data");
            var catalog = EnsureCatalog();

            // Prefab roots are created in whatever scene is open, so use a throwaway one.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefabs = BuildPrefabs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(prefabs, catalog);
            EditorSceneManager.SaveScene(scene, ScenePath);

            ConfigureProject();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Rovyn] UI built. Open Assets/Rovyn/Scenes/Rovyn.unity and press Play.");
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static ServerCatalog EnsureCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ServerCatalog>(CatalogPath);
            if (existing != null) return existing;

            var catalog = ScriptableObject.CreateInstance<ServerCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        // Scene order, a resizable 1280x800 window, and keep running when unfocused (voice keeps working).
        static void ConfigureProject()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
        }

        static T SavePrefab<T>(GameObject root, string name) where T : Component
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<T>();
        }

        // ---- Prefabs ----

        static Prefabs BuildPrefabs()
        {
            var voiceUser = BuildVoiceUserEntry();
            return new Prefabs
            {
                serverButton = SavePrefab<ServerButton>(MakeServerButton(null, "Server Button", false).gameObject, "ServerButton"),
                channelEntry = BuildChannelEntry(voiceUser),
                dmEntry = BuildDmEntry(),
                friendRow = BuildFriendRow(),
                messageItem = BuildMessageItem(),
                participantTile = BuildParticipantTile(),
                activeNowCard = BuildActiveNowCard(),
            };
        }

        // 40 px round button on the rail. The home variant shows the logo instead of initials.
        static ServerButton MakeServerButton(Transform parent, string name, bool home)
        {
            var root = Kit.Rect(name, parent);
            root.sizeDelta = new Vector2(40, 40);
            Kit.Elem(root.gameObject, w: 40, h: 40);

            var icon = Kit.Img(root, "Icon", Theme.Sidebar, Kit.Circle, raycast: true);
            Kit.Stretch(icon.rectTransform);

            Graphic glyph;
            TMP_Text initials = null;
            if (home)
            {
                var logo = Kit.Icon(root, "Logo", "logo", 24, Theme.TextNormal);
                Kit.Center(logo.rectTransform, 24, 24);
                glyph = logo;
            }
            else
            {
                initials = Kit.Label(root, "Initials", "", 14, Theme.TextNormal, FontStyles.Bold, TextAlignmentOptions.Center);
                Kit.Stretch(initials.rectTransform);
                glyph = initials;
            }

            var pill = Kit.Box(root, "Pill", Color.white, 2);
            Kit.Place(pill.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 4, 0, -16, 0);
            pill.gameObject.SetActive(false);

            var badge = Kit.Box(root, "Badge", Theme.DangerButton, 8);
            Kit.Place(badge.rectTransform, new Vector2(1, 0), new Vector2(1, 0), 22, 16, 4, -4);
            var badgeText = Kit.Label(badge.transform, "Count", "0", 11, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Kit.Stretch(badgeText.rectTransform);
            badge.gameObject.SetActive(false);

            var button = root.gameObject.AddComponent<ServerButton>();
            button.icon = icon;
            button.glyph = glyph;
            button.initials = initials;
            button.pill = pill.rectTransform;
            button.circleSprite = Kit.Circle;
            button.roundedSprite = Kit.Rounded;
            button.badge = badge.gameObject;
            button.badgeText = badgeText;
            button.normalColor = Theme.Sidebar;
            button.activeColor = home ? Theme.Brand : Theme.Accent;
            return button;
        }

        static VoiceUserEntry BuildVoiceUserEntry()
        {
            var root = Kit.Rect("Voice User Entry", null);
            Kit.HBox(root.gameObject, 8, Kit.Pad(8, 8, 2, 2));
            Kit.Elem(root.gameObject, h: 28);

            var avatar = Kit.MakeAvatar(root, "Avatar", 20, speaking: true);
            var name = Kit.Label(root, "Name", "", 14, Theme.TextMuted);
            Kit.Elem(name.gameObject, fw: 1);
            var muted = Kit.Icon(root, "Muted", "mic_off", 14, Theme.Red);
            muted.gameObject.SetActive(false);

            var entry = root.gameObject.AddComponent<VoiceUserEntry>();
            entry.avatar = avatar;
            entry.nameText = name;
            entry.mutedIcon = muted.gameObject;
            return SavePrefab<VoiceUserEntry>(root.gameObject, "VoiceUserEntry");
        }

        static ChannelEntry BuildChannelEntry(VoiceUserEntry voiceUser)
        {
            var root = Kit.Rect("Channel Entry", null);
            Kit.VBox(root.gameObject, 0, Kit.Pad(8, 8, 0, 0));

            var row = Kit.Box(root, "Row", Theme.Clear, 4, raycast: true);
            Kit.Elem(row.gameObject, h: 32);
            Kit.HBox(row.gameObject, 6, Kit.Pad(8, 8, 0, 0));
            var button = Kit.Btn(row.gameObject, row);

            var icon = Kit.Icon(row.transform, "Icon", "hash", 20, Theme.TextMuted);
            var label = Kit.Label(row.transform, "Label", "channel", 15, Theme.TextMuted);
            Kit.Elem(label.gameObject, fw: 1);
            var highlight = Kit.Hover(row.gameObject, row, new Graphic[] { icon, label });

            var pill = Kit.Box(row.transform, "Unread Pill", Color.white, 2);
            Kit.Elem(pill.gameObject, ignore: true);
            Kit.Place(pill.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 4, 8, -8, 0);
            pill.gameObject.SetActive(false);

            var users = Kit.Rect("Users", root);
            Kit.VBox(users.gameObject, 0, Kit.Pad(24, 0, 0, 4));
            users.gameObject.SetActive(false);

            var entry = root.gameObject.AddComponent<ChannelEntry>();
            entry.button = button;
            entry.highlight = highlight;
            entry.icon = icon;
            entry.label = label;
            entry.unreadPill = pill.gameObject;
            entry.usersContainer = users;
            entry.userPrefab = voiceUser;
            return SavePrefab<ChannelEntry>(root.gameObject, "ChannelEntry");
        }

        static DmEntry BuildDmEntry()
        {
            var root = Kit.Box(null, "Dm Entry", Theme.Clear, 4, raycast: true);
            Kit.Elem(root.gameObject, h: 44);
            Kit.HBox(root.gameObject, 12, Kit.Pad(8, 8, 0, 0));
            var button = Kit.Btn(root.gameObject, root);

            var avatar = Kit.MakeAvatar(root.transform, "Avatar", 32, status: true, statusRing: Theme.Sidebar);

            var texts = Kit.Rect("Texts", root.transform);
            Kit.VBox(texts.gameObject, 0, null, TextAnchor.MiddleLeft);
            Kit.Elem(texts.gameObject, fw: 1);
            var name = Kit.Label(texts, "Name", "", 15, Theme.TextMuted);
            var subtitle = Kit.Label(texts, "Subtitle", "", 12, Theme.TextMuted);
            subtitle.gameObject.SetActive(false);

            var badge = Kit.Box(root.transform, "Unread Badge", Theme.DangerButton, 8);
            Kit.Elem(badge.gameObject, minW: 16, h: 16);
            Kit.HBox(badge.gameObject, 0, Kit.Pad(5, 5, 0, 0), TextAnchor.MiddleCenter);
            var badgeText = Kit.Label(badge.transform, "Count", "0", 11, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            badge.gameObject.SetActive(false);

            var close = Kit.IconButton(root.transform, "Close", "close", 20, 14, Theme.Clear, Theme.Interactive);
            var highlight = Kit.Hover(root.gameObject, root, new Graphic[] { name }, new[] { close.gameObject });

            var entry = root.gameObject.AddComponent<DmEntry>();
            entry.button = button;
            entry.highlight = highlight;
            entry.avatar = avatar;
            entry.nameText = name;
            entry.subtitle = subtitle;
            entry.unreadBadge = badge.gameObject;
            entry.unreadText = badgeText;
            entry.closeButton = close;
            return SavePrefab<DmEntry>(root.gameObject, "DmEntry");
        }

        static FriendRow BuildFriendRow()
        {
            var root = Kit.Box(null, "Friend Row", Theme.Clear, 0, raycast: true);
            root.sprite = null;
            Kit.Elem(root.gameObject, h: 60);
            Kit.HBox(root.gameObject, 12, Kit.Pad(20, 20, 0, 0));
            var rowButton = Kit.Btn(root.gameObject, root);

            var avatar = Kit.MakeAvatar(root.transform, "Avatar", 32, status: true, statusRing: Theme.Main);

            var info = Kit.Rect("Info", root.transform);
            Kit.VBox(info.gameObject, 2, null, TextAnchor.MiddleLeft);
            Kit.Elem(info.gameObject, fw: 1);
            var nameRow = Kit.Rect("Name Row", info);
            Kit.HBox(nameRow.gameObject, 6, null, TextAnchor.LowerLeft);
            var name = Kit.Label(nameRow, "Name", "", 16, Theme.TextHeader, FontStyles.Bold);
            var tag = Kit.Label(nameRow, "Tag", "", 14, Theme.TextMuted);
            var subtitle = Kit.Label(info, "Subtitle", "", 13, Theme.TextMuted);

            var actionBg = Theme.Sidebar;
            var message = Kit.IconButton(root.transform, "Message", "message", 36, 20, actionBg, Theme.Interactive);
            var call = Kit.IconButton(root.transform, "Call", "phone", 36, 20, actionBg, Theme.Interactive);
            var more = Kit.IconButton(root.transform, "More", "more_v", 36, 20, actionBg, Theme.Interactive);
            var accept = Kit.IconButton(root.transform, "Accept", "check", 36, 20, actionBg, Theme.Green);
            var decline = Kit.IconButton(root.transform, "Decline", "close", 36, 20, actionBg, Theme.Red);
            var cancel = Kit.IconButton(root.transform, "Cancel", "close", 36, 20, actionBg, Theme.Red);
            var unblock = Kit.TextButton(root.transform, "Unblock", "Unblock", Theme.Composer, Color.white, 32, 80);

            // The #1234 part only shows while the row is hovered.
            Kit.Hover(root.gameObject, root, new Graphic[] { tag }, fgNormal: Theme.Clear, fgHover: Theme.TextMuted, fgSelected: Theme.TextMuted);

            var divider = Kit.Box(root.transform, "Divider", Theme.Divider);
            Kit.Elem(divider.gameObject, ignore: true);
            Kit.Strip(divider.rectTransform, 0, 1);
            divider.rectTransform.offsetMin = new Vector2(20, 0);
            divider.rectTransform.offsetMax = new Vector2(-20, 1);

            var row = root.gameObject.AddComponent<FriendRow>();
            row.rowButton = rowButton;
            row.avatar = avatar;
            row.nameText = name;
            row.tagText = tag;
            row.subtitle = subtitle;
            row.messageButton = message;
            row.callButton = call;
            row.moreButton = more;
            row.acceptButton = accept;
            row.declineButton = decline;
            row.cancelButton = cancel;
            row.unblockButton = unblock;
            return SavePrefab<FriendRow>(root.gameObject, "FriendRow");
        }

        static MessageItem BuildMessageItem()
        {
            var root = Kit.Box(null, "Message Item", Theme.Clear, 0, raycast: true);
            Kit.Elem(root.gameObject, fh: 0);
            var layout = Kit.HBox(root.gameObject, 16, Kit.Pad(16, 16, 16, 2), TextAnchor.UpperLeft, expandH: true);

            var left = Kit.Rect("Left", root.transform);
            Kit.Elem(left.gameObject, w: 40);
            var avatar = Kit.MakeAvatar(left, "Avatar", 40);
            Kit.Place(avatar.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), 40, 40);
            var timeHolder = Kit.Rect("Hover Time Holder", left);
            Kit.Stretch(timeHolder);
            var hoverTime = Kit.Label(timeHolder, "Hover Time", "", 11, Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.TopRight);
            Kit.Stretch(hoverTime.rectTransform, 0, 0, 0, 4);

            var right = Kit.Rect("Right", root.transform);
            Kit.VBox(right.gameObject, 2);
            Kit.Elem(right.gameObject, fw: 1);
            var header = Kit.Rect("Header", right);
            Kit.HBox(header.gameObject, 8, null, TextAnchor.LowerLeft);
            var author = Kit.Label(header, "Author", "", 16, Theme.TextHeader, FontStyles.Bold);
            author.raycastTarget = true;
            var authorButton = Kit.Btn(author.gameObject, author);
            var timestamp = Kit.Label(header, "Timestamp", "", 12, Theme.TextMuted);
            var body = Kit.Label(right, "Body", "", 16, Theme.TextNormal, FontStyles.Normal, TextAlignmentOptions.TopLeft, wrap: true);

            Kit.Hover(root.gameObject, root, new Graphic[] { hoverTime }, null,
                fgNormal: Theme.Clear, fgHover: Theme.TextMuted, fgSelected: Theme.TextMuted,
                hover: Theme.MessageHover, selected: Theme.MessageHover);

            var item = root.gameObject.AddComponent<MessageItem>();
            item.layout = layout;
            item.header = header.gameObject;
            item.avatar = avatar;
            item.author = author;
            item.timestamp = timestamp;
            item.body = body;
            item.hoverTime = hoverTime;
            item.authorButton = authorButton;
            return SavePrefab<MessageItem>(root.gameObject, "MessageItem");
        }

        static ParticipantTile BuildParticipantTile()
        {
            var root = Kit.Box(null, "Participant Tile", Theme.Rail, 12, raycast: true);
            var button = Kit.Btn(root.gameObject, root);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var avatar = Kit.MakeAvatar(root.transform, "Avatar", 80, speaking: true);
            Kit.Center(avatar.GetComponent<RectTransform>(), 80, 80);

            var name = Kit.Label(root.transform, "Name", "", 14, Color.white, FontStyles.Bold);
            Kit.Place(name.rectTransform, new Vector2(0, 0), new Vector2(0, 0), 260, 24, 14, 10);

            var muted = Kit.Img(root.transform, "Muted", Theme.DangerButton, Kit.Circle);
            Kit.Place(muted.rectTransform, new Vector2(1, 0), new Vector2(1, 0), 24, 24, -10, 10);
            var mutedIcon = Kit.Icon(muted.transform, "Icon", "mic_off", 14, Color.white);
            Kit.Center(mutedIcon.rectTransform, 14, 14);
            muted.gameObject.SetActive(false);

            var outline = Kit.Img(root.transform, "Speaking Outline", Theme.Green, Kit.RoundedOutline, 12);
            Kit.Stretch(outline.rectTransform);
            outline.enabled = false;

            var tile = root.gameObject.AddComponent<ParticipantTile>();
            tile.button = button;
            tile.background = root;
            tile.avatar = avatar;
            tile.nameText = name;
            tile.mutedIcon = muted.gameObject;
            tile.speakingOutline = outline;
            tile.group = group;
            return SavePrefab<ParticipantTile>(root.gameObject, "ParticipantTile");
        }

        static ActiveNowCard BuildActiveNowCard()
        {
            var root = Kit.Box(null, "Active Now Card", Theme.Card, 8, raycast: true);
            Kit.HBox(root.gameObject, 12, Kit.Pad(12));
            var button = Kit.Btn(root.gameObject, root);

            var avatar = Kit.MakeAvatar(root.transform, "Avatar", 40);
            var texts = Kit.Rect("Texts", root.transform);
            Kit.VBox(texts.gameObject, 2, null, TextAnchor.MiddleLeft);
            Kit.Elem(texts.gameObject, fw: 1);
            var name = Kit.Label(texts, "Name", "", 15, Theme.TextHeader, FontStyles.Bold);
            var activity = Kit.Label(texts, "Activity", "", 13, Theme.TextMuted);
            var join = Kit.TextButton(root.transform, "Join", "Join", Theme.GreenButton, Color.white, 28, 56, 4, 13);

            var card = root.gameObject.AddComponent<ActiveNowCard>();
            card.cardButton = button;
            card.avatar = avatar;
            card.nameText = name;
            card.activityText = activity;
            card.joinButton = join;
            return SavePrefab<ActiveNowCard>(root.gameObject, "ActiveNowCard");
        }
    }
}
