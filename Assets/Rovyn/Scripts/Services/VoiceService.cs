using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

namespace Rovyn
{
    // Voice through Vivox. One voice connection at a time, like Discord:
    // joining a server voice channel or a private call leaves the previous one.
    public sealed class VoiceService
    {
        public event Action StateChanged;
        public event Action ParticipantsChanged;
        public event Action DevicesChanged;

        public string Channel { get; private set; }  // Vivox channel name
        public string Label { get; private set; }    // what the UI shows, e.g. "Lounge"
        public bool IsConnecting { get; private set; }
        public bool IsReconnecting { get; private set; }
        public bool IsConnected => Channel != null && !IsConnecting;
        public bool IsPrivate { get; private set; }
        public bool IsMuted { get; private set; }
        public bool IsDeafened { get; private set; }
        public bool IsMicTesting { get; private set; }
        internal VoiceCaptureSettings Capture { get; } = new();

        static IVivoxService Service => VivoxService.Instance;
        static readonly IReadOnlyList<VivoxParticipant> NoParticipants = Array.Empty<VivoxParticipant>();

        const string MutedPref = "rovyn.voice.muted";
        const string DeafenedPref = "rovyn.voice.deafened";
        const string InputDevicePref = "rovyn.voice.input";
        const string OutputDevicePref = "rovyn.voice.output";
        const string InputVolumePref = "rovyn.voice.inputVolume";
        const string OutputVolumePref = "rovyn.voice.outputVolume";
        static string UserVolumePref(string id) => "rovyn.voice.user." + id;

        bool _leaving;
        int _channelVersion;
        readonly SemaphoreSlim _channelChange = new(1, 1);
        readonly HashSet<string> _mutedPlayers = new();

        public IReadOnlyList<VivoxParticipant> Participants =>
            Channel != null && Service.ActiveChannels.TryGetValue(Channel, out var list) ? list : NoParticipants;

        public void Start()
        {
            Service.ChannelJoined += OnChannelJoined;
            Service.ChannelLeft += OnChannelLeft;
            Service.ParticipantAddedToChannel += OnParticipantAdded;
            Service.ParticipantRemovedFromChannel += OnParticipantRemoved;
            Service.AvailableInputDevicesChanged += OnDevicesChanged;
            Service.AvailableOutputDevicesChanged += OnDevicesChanged;
            Service.EffectiveOutputDeviceChanged += OnEffectiveOutputChanged;
            App.Vivox.LoggedIn += ApplySavedSettings;
            App.Vivox.StateChanged += OnConnectionChanged;
            IsMuted = PlayerPrefs.GetInt(MutedPref, 0) == 1;
            IsDeafened = PlayerPrefs.GetInt(DeafenedPref, 0) == 1;
        }

        public void Stop()
        {
            Service.ChannelJoined -= OnChannelJoined;
            Service.ChannelLeft -= OnChannelLeft;
            Service.ParticipantAddedToChannel -= OnParticipantAdded;
            Service.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            Service.AvailableInputDevicesChanged -= OnDevicesChanged;
            Service.AvailableOutputDevicesChanged -= OnDevicesChanged;
            Service.EffectiveOutputDeviceChanged -= OnEffectiveOutputChanged;
            App.Vivox.LoggedIn -= ApplySavedSettings;
            App.Vivox.StateChanged -= OnConnectionChanged;
            ClearChannel();
        }

        // ---- Channels ----

        // Private channels (calls) are shown to friends as "Private call" without a joinable channel.
        public async UniTask JoinAsync(string channel, string label, bool isPrivate = false)
        {
            await _channelChange.WaitAsync();
            try
            {
                if (Channel == channel) return;
                if (App.Vivox.State != ConnectionState.Connected) throw new InvalidOperationException("Not connected to voice.");
                await StopMicTestAsync();
                await LeaveChannelAsync();
                if (!App.SessionActive || App.Vivox.State != ConnectionState.Connected)
                    throw new InvalidOperationException("Not connected to voice.");

                Channel = channel;
                Label = label;
                IsPrivate = isPrivate;
                IsConnecting = true;
                StateChanged?.Invoke();
                int version = _channelVersion;
                try
                {
                    await JoinAudioAsync(channel);
                }
                catch
                {
                    if (version == _channelVersion) ClearChannel();
                    throw;
                }
                if (version != _channelVersion) return;
                IsConnecting = false;
                StateChanged?.Invoke();
                ParticipantsChanged?.Invoke();
                if (isPrivate) App.Friends.SetVoiceActivityAsync("Private call", null).Forget();
                else App.Friends.SetVoiceActivityAsync(label, channel).Forget();
            }
            finally { _channelChange.Release(); }
        }

        public async UniTask LeaveAsync(string expectedChannel = null)
        {
            await _channelChange.WaitAsync();
            try
            {
                if (expectedChannel == null || expectedChannel == Channel) await LeaveChannelAsync();
            }
            finally { _channelChange.Release(); }
        }

        async UniTask LeaveChannelAsync()
        {
            if (Channel == null) return;
            var channel = Channel;
            _leaving = true;
            try
            {
                if (App.Vivox.State == ConnectionState.Recovering)
                {
                    ClearChannel();
                    await UniTask.WaitUntil(() => App.Vivox.State != ConnectionState.Recovering || !App.SessionActive);
                }
                if (App.Vivox.State == ConnectionState.Connected && Service.ActiveChannels.ContainsKey(channel))
                    await Service.LeaveChannelAsync(channel);
            }
            catch (Exception e) { Debug.LogWarning($"Leaving voice failed: {e.Message}"); }
            finally { _leaving = false; }
            ClearChannel();
        }

        void ClearChannel()
        {
            bool had = Channel != null;
            Channel = null;
            Label = null;
            _channelVersion++;
            IsConnecting = false;
            IsReconnecting = false;
            IsPrivate = false;
            _mutedPlayers.Clear();
            if (!had) return;
            StateChanged?.Invoke();
            ParticipantsChanged?.Invoke();
            if (App.Friends.IsReady) App.Friends.SetVoiceActivityAsync(null, null).Forget();
        }

        void OnChannelJoined(string channel)
        {
            if (channel == Channel) ParticipantsChanged?.Invoke();
        }

        void OnChannelLeft(string channel)
        {
            if (IsMicTesting && channel == EchoChannel)
            {
                IsMicTesting = false;
                StateChanged?.Invoke();
                return;
            }
            if (channel != Channel || _leaving || IsConnecting) return;
            if (App.Vivox.IsLoggedIn && App.SessionActive)
            {
                IsConnecting = true;
                IsReconnecting = true;
                StateChanged?.Invoke();
                ParticipantsChanged?.Invoke();
                Debug.LogWarning("Vivox audio channel disconnected. Rejoining current voice session.");
                RejoinAsync(channel, _channelVersion).Forget();
            }
            else ClearChannel();
        }

        async UniTask JoinAudioAsync(string channel)
        {
            if (!Service.ActiveChannels.ContainsKey(channel))
                await Service.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
            if (!Service.ActiveChannels.ContainsKey(channel))
                throw new InvalidOperationException("Vivox did not connect to the audio channel.");
            RestoreOutputState();
        }

        async UniTaskVoid RejoinAsync(string channel, int version)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1 << attempt), ignoreTimeScale: true);
                await UniTask.WaitUntil(() => version != _channelVersion || App.Vivox.State != ConnectionState.Recovering);
                await _channelChange.WaitAsync();
                try
                {
                    if (version != _channelVersion || !App.SessionActive) return;
                    if (App.Vivox.State != ConnectionState.Connected) continue;
                    await JoinAudioAsync(channel);
                    if (version != _channelVersion) return;
                    IsConnecting = false;
                    IsReconnecting = false;
                    StateChanged?.Invoke();
                    ParticipantsChanged?.Invoke();
                    return;
                }
                catch (Exception e) { Debug.LogWarning($"Vivox audio recovery attempt {attempt + 1}: {e.Message}"); }
                finally { _channelChange.Release(); }
            }
            if (version != _channelVersion) return;
            ClearChannel();
            App.Report("Couldn't reconnect to voice.");
        }

        void OnConnectionChanged()
        {
            Capture.QueueApply();
            if (App.Vivox.State == ConnectionState.Connected && Channel != null)
                RestoreOutputState();
            // Vivox rejoins channels itself while recovering. If recovery fails the channel is gone.
            if (App.Vivox.State == ConnectionState.Failed || App.Vivox.State == ConnectionState.Offline)
            {
                ClearChannel();
                IsMicTesting = false;
            }
        }

        void OnParticipantAdded(VivoxParticipant p)
        {
            if (IsMicTesting && p.IsSelf && p.ChannelName == EchoChannel)
                StateChanged?.Invoke();
            if (p.ChannelName != Channel) return;
            if (!p.IsSelf && PlayerPrefs.HasKey(UserVolumePref(p.PlayerId)))
                p.SetLocalVolume(PlayerPrefs.GetInt(UserVolumePref(p.PlayerId)));
            if (_mutedPlayers.Contains(p.PlayerId)) p.MutePlayerLocally();
            ParticipantsChanged?.Invoke();
        }

        void OnParticipantRemoved(VivoxParticipant p)
        {
            if (IsMicTesting && p.IsSelf && p.ChannelName == EchoChannel)
                StateChanged?.Invoke();
            if (p.ChannelName == Channel) ParticipantsChanged?.Invoke();
        }

        // ---- Mute / deafen ----

        public void SetMuted(bool muted)
        {
            // Unmuting while deafened also undeafens, same as Discord.
            if (!muted && IsDeafened) IsDeafened = false;
            IsMuted = muted;
            ApplyMuteState();
        }

        public void SetDeafened(bool deafened)
        {
            IsDeafened = deafened;
            ApplyMuteState();
        }

        void ApplyMuteState()
        {
            PlayerPrefs.SetInt(MutedPref, IsMuted ? 1 : 0);
            PlayerPrefs.SetInt(DeafenedPref, IsDeafened ? 1 : 0);
            if (App.Vivox.IsLoggedIn)
            {
                // Deafened always means the mic is off too.
                if (IsMuted || IsDeafened) Service.MuteInputDevice(); else Service.UnmuteInputDevice();
                if (IsDeafened) Service.MuteOutputDevice(); else Service.UnmuteOutputDevice();
            }
            StateChanged?.Invoke();
        }

        // ---- Per-user controls (local only, nobody else is affected) ----

        public void SetUserVolume(VivoxParticipant p, int volume)
        {
            volume = Mathf.Clamp(volume, -50, 50);
            p.SetLocalVolume(volume);
            PlayerPrefs.SetInt(UserVolumePref(p.PlayerId), volume);
        }

        public void SetUserMuted(VivoxParticipant p, bool muted)
        {
            if (muted)
            {
                p.MutePlayerLocally();
                _mutedPlayers.Add(p.PlayerId);
            }
            else
            {
                p.UnmutePlayerLocally();
                _mutedPlayers.Remove(p.PlayerId);
            }
        }

        // ---- Devices and volume ----

        public IReadOnlyList<VivoxInputDevice> InputDevices =>
            App.Vivox.IsLoggedIn ? (IReadOnlyList<VivoxInputDevice>)Service.AvailableInputDevices : Array.Empty<VivoxInputDevice>();
        public IReadOnlyList<VivoxOutputDevice> OutputDevices =>
            App.Vivox.IsLoggedIn ? (IReadOnlyList<VivoxOutputDevice>)Service.AvailableOutputDevices : Array.Empty<VivoxOutputDevice>();
        public VivoxInputDevice ActiveInput => App.Vivox.IsLoggedIn ? Service.ActiveInputDevice : null;
        public VivoxOutputDevice ActiveOutput => App.Vivox.IsLoggedIn ? Service.ActiveOutputDevice : null;

        // Vivox volume range is -50..50 with 0 as the default.
        public int InputVolume => PlayerPrefs.GetInt(InputVolumePref, 0);
        public int OutputVolume => PlayerPrefs.GetInt(OutputVolumePref, 0);

        public async UniTask SetInputDeviceAsync(VivoxInputDevice device)
        {
            await Service.SetActiveInputDeviceAsync(device);
            PlayerPrefs.SetString(InputDevicePref, device.DeviceID);
        }

        public async UniTask SetOutputDeviceAsync(VivoxOutputDevice device)
        {
            await Service.SetActiveOutputDeviceAsync(device);
            PlayerPrefs.SetString(OutputDevicePref, device.DeviceID);
            RestoreOutputState();
        }

        public void SetInputVolume(int value)
        {
            value = Mathf.Clamp(value, -50, 50);
            PlayerPrefs.SetInt(InputVolumePref, value);
            if (App.Vivox.IsLoggedIn) Service.SetInputDeviceVolume(value);
        }

        public void SetOutputVolume(int value)
        {
            value = Mathf.Clamp(value, -50, 50);
            PlayerPrefs.SetInt(OutputVolumePref, value);
            if (App.Vivox.IsLoggedIn) Service.SetOutputDeviceVolume(value);
        }

        void OnDevicesChanged() => DevicesChanged?.Invoke();

        void OnEffectiveOutputChanged()
        {
            if (Channel != null || IsMicTesting)
            {
                Debug.Log("Vivox playback device changed during voice.");
                RestoreOutputState();
            }
            DevicesChanged?.Invoke();
        }

        void RestoreOutputState()
        {
            if (App.Vivox.State != ConnectionState.Connected || !Service.IsLoggedIn) return;
            try
            {
                Service.SetOutputDeviceVolume(OutputVolume);
                if (Service.IsOutputDeviceMuted != IsDeafened)
                {
                    if (IsDeafened) Service.MuteOutputDevice();
                    else Service.UnmuteOutputDevice();
                }
            }
            catch (Exception e) { Debug.LogWarning($"Restoring Vivox output failed: {e.Message}"); }
        }

        void ApplySavedSettings() => ApplySavedSettingsAsync().Forget();

        async UniTaskVoid ApplySavedSettingsAsync()
        {
            Service.SetInputDeviceVolume(InputVolume);
            Service.SetOutputDeviceVolume(OutputVolume);
            ApplyMuteState();
            try
            {
                var input = Service.AvailableInputDevices.FirstOrDefault(d => d.DeviceID == PlayerPrefs.GetString(InputDevicePref, ""));
                if (input != null) await Service.SetActiveInputDeviceAsync(input);
                var output = Service.AvailableOutputDevices.FirstOrDefault(d => d.DeviceID == PlayerPrefs.GetString(OutputDevicePref, ""));
                if (output != null) await Service.SetActiveOutputDeviceAsync(output);
            }
            catch (Exception e) { Debug.LogWarning($"Restoring audio devices failed: {e.Message}"); }
            DevicesChanged?.Invoke();
        }

        // ---- Mic test: an echo channel plays your own voice back to you ----

        string EchoChannel => "echo." + App.Auth.PlayerId;

        public async UniTask StartMicTestAsync()
        {
            if (IsMicTesting || Channel != null) return;
            await Service.JoinEchoChannelAsync(EchoChannel, ChatCapability.AudioOnly);
            IsMicTesting = true;
            StateChanged?.Invoke();
        }

        public async UniTask StopMicTestAsync()
        {
            if (!IsMicTesting) return;
            IsMicTesting = false;
            try
            {
                if (Service.ActiveChannels.ContainsKey(EchoChannel))
                    await Service.LeaveChannelAsync(EchoChannel);
            }
            catch (Exception e) { Debug.LogWarning($"Stopping mic test failed: {e.Message}"); }
            StateChanged?.Invoke();
        }

        // Self participant in the echo channel, for the mic level meter.
        public VivoxParticipant MicTestSelf =>
            IsMicTesting && Service.ActiveChannels.TryGetValue(EchoChannel, out var list) ? list.FirstOrDefault(p => p.IsSelf) : null;
    }
}
