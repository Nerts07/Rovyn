# Rovyn setup notes

Rovyn uses Unity Authentication for accounts, Friends for relationships and presence, Vivox for text and voice, and Cloud Save for profile photos.

## Folder layout

- `Scripts/Services` holds the service layer. `App.cs` owns one instance of each service and the signed-in session lifecycle. Nothing in this folder touches UI.
- `Scripts/UI` holds the views. Each screen or list item is a MonoBehaviour with serialized references, wired in the scene or prefab.
- `Editor` holds the one-time UI builder (menu **Rovyn > Build UI (Scene + Prefabs)**). It uses the SVG icons and shapes in `UI/Icons` and `UI/Shapes`.
- `Prefabs`, `Scenes/Rovyn.unity` and `Data/ServerCatalog.asset` are created by the builder. After that they're normal assets you edit in the Editor.

## First run in the Editor

1. Let Unity resolve the new packages in `Packages/manifest.json` (UGS Core 1.19.0, Authentication 3.9.0, Friends 1.4.1, Vivox 16.13.0, UniTask 2.5.11 from GitHub). UniTask comes from a Git URL, so Git has to be on your PATH.
2. Import **Window > TextMeshPro > Import TMP Essential Resources** if you haven't. The builder stops and tells you if they're missing.
3. Run **Rovyn > Build UI (Scene + Prefabs)**. It saves the prefabs, creates `Assets/Rovyn/Scenes/Rovyn.unity`, puts that scene first in Build Settings, and sets the Windows player to a resizable 1280x800 window that keeps running in the background. Running it again overwrites the scene and prefabs (it asks first) but keeps your server catalog.
4. Open `Rovyn.unity` and press Play.

## Unity Cloud Dashboard

The project is already linked to a cloud project (`ProjectSettings > cloudProjectId`). In the Dashboard, for that project and the environment you build against (`production` unless you change it):

1. **Player Authentication > Identity providers**: add the **Username & Password** provider. Without it, sign-up returns an error.
2. **Friends**: open the Friends product page and enable it for the project. Until you do, the app logs you in and shows "Friends is not enabled for this project".
3. **Vivox Voice and Text**: set it up for the project. The Vivox package picks up the project link on its own and gets its access tokens from UGS Authentication, so no Vivox key goes in the code.
4. In the Editor, check **Project Settings > Services > Vivox** and leave **Test Mode** off for anything you ship. Test mode embeds the Vivox secret in the client.
5. **Cloud Save**: enable it in the same environment for public profile photos. Rovyn creates the public `rovyn_avatar` entry for the signed-in player on their first upload. [Cloud Save Player Data](https://docs.unity.com/en-us/cloud-save/concepts/player-data)

Player names (the `Name#1234` format used for friend requests) come with Authentication and need no extra step.

## What's connected

- **Accounts.** Sign-up, login, logout, saved sessions (auto sign-in on launch through the stored session token), display name and password changes. UGS username/password has no email or recovery flow, so neither exists in the app. Validation follows the UGS rules: usernames 3-20 characters, passwords 8-30 with upper, lower, digit and symbol.
- **Profile avatars.** In the Windows app, open **Settings > My Account > Upload avatar** and choose a PNG or JPEG under 10 MB and 32 megapixels. Rovyn center-crops it to a 128 × 128 image and stores it in Cloud Save, where other signed-in users can read it. Open views refresh roughly once a minute.
- **Friends.** Requests by full name (`Name#1234`) or player ID, accept, ignore, cancel, remove, block and unblock, all live through the Friends event stream. Presence covers Online, Idle, Do Not Disturb and Invisible, plus a custom status and the voice channel you're in. Blocking someone also blocks them in Vivox.
- **Text.** Vivox direct messages and server text channels, paged history, a DM list rebuilt at login, and unread counts based on Vivox read flags. Senders can delete their own messages for everyone. Other messages can be hidden on this device.
- **Voice.** Server voice channels, private calls above the DM history, mute, deafen, input/output devices and volume, a mic test through a Vivox echo channel, per-user volume and local mute (saved per player), and speaking indicators. Completed calls show their duration in the DM on this device. Rovyn tries to rejoin the audio channel if Vivox drops it while the session is still available.

## Limits worth knowing

**Servers aren't private.** They come from `Data/ServerCatalog.asset`, and any signed-in player who knows a channel name can join it, because Vivox channels with UGS tokens are open by name. Real membership and moderation need a small backend: a Cloud Code module that keeps server membership (Cloud Save works for storage) and signs Vivox join tokens server-side, plugged into the client with `VivoxService.Instance.SetTokenProvider`. The same applies to private call channels, which are named after both player IDs and can be guessed. Ringing only works between friends, since it goes through Friends messaging.

**Unread tracking.** DM unread counts look at the last 20 messages of the 20 most recent conversations at login. Only one text channel is joined at a time, so new messages in other server channels aren't counted until you open them.

**Offline messages.** Vivox keeps DM history on its side. Messages sent while a friend was offline should appear in that conversation's history the next time they open it, as history rather than as a live notification. Worth testing with two accounts before relying on it.

**Presence on quit.** Closing the window sets you offline with a 3-second limit. If the app crashes, friends see you online until the Friends service notices.

**Not built.** Typing indicators, message editing, and a minimum window size.
