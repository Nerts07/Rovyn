using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Rovyn
{
    // Owns the service instances and the signed-in session lifecycle.
    // UI reads services from here; services never reference UI.
    public static class App
    {
        public static readonly AuthService Auth = new();
        public static readonly FriendsStore Friends = new();
        public static readonly VivoxSession Vivox = new();
        public static readonly ChatService Chat = new();
        public static readonly VoiceService Voice = new();
        public static readonly CallService Calls = new();

        // Short user-facing notices (errors, confirmations). Shown by the Toast view.
        public static event Action<string> Notice;

        public static bool SessionActive { get; private set; }

        public static void Report(string message)
        {
            Debug.Log($"[Rovyn] {message}");
            Notice?.Invoke(message);
        }

        public static async UniTask InitializeAsync()
        {
            await Auth.InitializeAsync();
            // Vivox problems (e.g. not enabled in the Dashboard yet) shouldn't block logging in.
            // VivoxSession retries initialization when it connects.
            try { await Vivox.InitializeAsync(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // Runs once the player is signed in. Friends and Vivox both need a signed-in player.
        // A failure in one service doesn't block the others: the user still gets into the app
        // and sees the error, instead of being stuck on a loading screen.
        public static async UniTask StartSessionAsync()
        {
            if (SessionActive) return;
            SessionActive = true;

            try { await Friends.StartAsync(); }
            catch (Exception e) { Report($"Friends unavailable: {FriendsStore.Describe(e)}"); Debug.LogException(e); }

            Chat.Start();
            Voice.Start();
            Calls.Start();
            await Vivox.LoginAsync(Auth.DisplayName, Friends.BlockedIds());
        }

        public static async UniTask EndSessionAsync()
        {
            if (!SessionActive) return;
            SessionActive = false;

            Calls.Stop();
            await Voice.LeaveAsync();
            Voice.Stop();
            Chat.Stop();
            await Vivox.LogoutAsync();
            await Friends.StopAsync();
        }

        public static async UniTask SignOutAsync()
        {
            await EndSessionAsync();
            Auth.SignOut();
        }
    }
}
