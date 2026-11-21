using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

namespace Rovyn
{
    public enum ConnectionState { Offline, Connecting, Connected, Recovering, Failed }

    // Vivox login state shared by chat and voice.
    // Vivox picks up the UGS Authentication token on its own, so the client never holds a Vivox secret.
    public sealed class VivoxSession
    {
        public event Action StateChanged;
        public event Action LoggedIn;

        public ConnectionState State { get; private set; } = ConnectionState.Offline;
        public string LastError { get; private set; }
        public bool IsLoggedIn => State == ConnectionState.Connected || State == ConnectionState.Recovering;

        static IVivoxService Service => VivoxService.Instance;

        string _displayName;
        List<string> _blocked = new();
        bool _subscribed;

        public async UniTask InitializeAsync()
        {
            if (Service.InitializationState != VivoxInitializationState.Initialized)
                await Service.InitializeAsync();

            if (_subscribed) return;
            _subscribed = true;
            Service.LoggedOut += OnLoggedOut;
            Service.ConnectionRecovering += () => SetState(ConnectionState.Recovering);
            Service.ConnectionRecovered += () => SetState(ConnectionState.Connected);
            Service.ConnectionFailedToRecover += () => SetState(ConnectionState.Failed, "Lost connection to the voice and chat server.");
        }

        public async UniTask LoginAsync(string displayName, List<string> blockedIds)
        {
            _displayName = displayName;
            _blocked = blockedIds ?? new List<string>();
            await ConnectAsync();
        }

        // Also used by the "Reconnect" button after a failed recovery.
        public async UniTask ConnectAsync()
        {
            if (State == ConnectionState.Connecting) return;
            SetState(ConnectionState.Connecting);
            try
            {
                await InitializeAsync();
                if (Service.IsLoggedIn) await Service.LogoutAsync();
                var options = new LoginOptions
                {
                    DisplayName = _displayName,
                    // Speaking indicators need participant updates on every state change.
                    ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.StateChange,
                    BlockedUserList = new List<string>(_blocked),
                };
                await Service.LoginAsync(options);
                if (!Service.IsLoggedIn) throw new InvalidOperationException("Vivox login did not complete. Is the player signed in?");
                SetState(ConnectionState.Connected);
                LoggedIn?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetState(ConnectionState.Failed, "Couldn't connect to chat and voice. Check that Vivox is enabled for this project.");
            }
        }

        public async UniTask LogoutAsync()
        {
            // Set Offline first so the LoggedOut event isn't treated as a dropped connection.
            SetState(ConnectionState.Offline);
            try
            {
                if (Service.IsLoggedIn) await Service.LogoutAsync();
            }
            catch (Exception e) { Debug.LogWarning($"Vivox logout: {e.Message}"); }
        }

        // Vivox-level blocking stops audio and text from that player, matching a Friends block.
        public async UniTask BlockAsync(string playerId, bool block)
        {
            if (block) { if (!_blocked.Contains(playerId)) _blocked.Add(playerId); }
            else _blocked.Remove(playerId);

            if (!Service.IsLoggedIn) return;
            try
            {
                if (block) await Service.BlockPlayerAsync(playerId);
                else await Service.UnblockPlayerAsync(playerId);
            }
            catch (Exception e) { Debug.LogWarning($"Vivox block update failed: {e.Message}"); }
        }

        void OnLoggedOut()
        {
            // Planned logouts already moved us to Offline. Anything else is unexpected,
            // for example Vivox logging out because the UGS session ended.
            if (State == ConnectionState.Connected || State == ConnectionState.Recovering)
                SetState(ConnectionState.Failed, "Disconnected from chat and voice.");
        }

        void SetState(ConnectionState state, string error = null)
        {
            State = state;
            LastError = error;
            StateChanged?.Invoke();
        }
    }
}
