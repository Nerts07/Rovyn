using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Friends.Notifications;
using UnityEngine;

namespace Rovyn
{
    public enum CallState { None, Outgoing, Incoming, Active }

    // Call signaling payload sent through UGS Friends messaging. Public fields for Newtonsoft.
    [Serializable]
    public class CallSignal
    {
        public string type;    // ring, accept, decline, cancel, hangup
        public string channel; // Vivox channel both sides join
        public string callId;
    }

    // Private 1:1 voice calls. Ringing goes through Friends messages (friends only),
    // the audio itself goes through a Vivox channel named after both player IDs.
    public sealed class CallService
    {
        public event Action StateChanged;
        internal event Action<string> HistoryChanged;

        public CallState State { get; private set; }
        public string PeerId { get; private set; }

        const int RingTimeoutSeconds = 45;

        string _channel;
        string _callId;
        string _callerId;
        string _historyOwner;
        double? _connectedAt;
        int _callVersion; // invalidates stale ring timeouts
        int? _joiningVersion;
        bool _startingCall;

        internal bool IsCalling(string peerId) => PeerId == peerId && State is CallState.Outgoing or CallState.Active;

        public static string ChannelFor(string a, string b) =>
            string.CompareOrdinal(a, b) < 0 ? $"call.{a}.{b}" : $"call.{b}.{a}";

        public void Start()
        {
            App.Friends.MessageReceived += OnMessage;
            App.Voice.StateChanged += OnVoiceChanged;
            App.Voice.ParticipantsChanged += OnParticipantsChanged;
        }

        public void Stop()
        {
            App.Friends.MessageReceived -= OnMessage;
            App.Voice.StateChanged -= OnVoiceChanged;
            App.Voice.ParticipantsChanged -= OnParticipantsChanged;
            SetState(CallState.None, null);
        }

        public async UniTask StartCallAsync(string peerId)
        {
            if (_startingCall || IsCalling(peerId)) return;
            if (!App.Friends.IsFriend(peerId)) throw new InvalidOperationException("You can only call friends.");
            _startingCall = true;
            try
            {
                if (State != CallState.None) await HangUpAsync();
                if (!App.SessionActive) return;
                var channel = ChannelFor(App.Auth.PlayerId, peerId);
                _channel = channel;
                _callId = Guid.NewGuid().ToString("N");
                _callerId = App.Auth.PlayerId;
                SetState(CallState.Outgoing, peerId);
                int version = _callVersion;
                await ConnectCallAsync(peerId, channel, version, "ring");
                if (version == _callVersion) RingTimeoutAsync(version).Forget();
            }
            finally { _startingCall = false; }
        }

        public async UniTask AcceptAsync()
        {
            if (State != CallState.Incoming) return;
            var peer = PeerId;
            var channel = _channel;
            SetState(CallState.Active, peer);
            await ConnectCallAsync(peer, channel, _callVersion, "accept");
        }

        async UniTask ConnectCallAsync(string peer, string channel, int version, string signal)
        {
            _joiningVersion = version;
            try
            {
                await App.Voice.JoinAsync(channel, App.Friends.NameOf(peer), isPrivate: true);
                if (version != _callVersion) return;
                if (App.Voice.Channel != channel) throw new InvalidOperationException("Couldn't connect to call audio.");
                await App.Friends.SendMessageAsync(peer, new CallSignal { type = signal, channel = channel, callId = _callId });
            }
            catch
            {
                if (version != _callVersion) return;
                SetState(CallState.None, null);
                await App.Voice.LeaveAsync(channel);
                throw;
            }
            finally
            {
                if (_joiningVersion == version) _joiningVersion = null;
                if (version == _callVersion) OnVoiceChanged();
            }
        }

        public async UniTask DeclineAsync()
        {
            if (State != CallState.Incoming) return;
            await HangUpAsync();
        }

        public async UniTask HangUpAsync()
        {
            if (State == CallState.None) return;
            var peer = PeerId;
            var channel = _channel;
            var callId = _callId;
            var signal = State switch
            {
                CallState.Outgoing => "cancel",
                CallState.Incoming => "decline",
                _ => "hangup",
            };
            SetState(CallState.None, null);
            var send = Signal(peer, channel, callId, signal);
            if (signal != "decline") await App.Voice.LeaveAsync(channel);
            await send;
        }

        async UniTask Signal(string peer, string channel, string callId, string type)
        {
            if (string.IsNullOrEmpty(peer)) return;
            try { await App.Friends.SendMessageAsync(peer, new CallSignal { type = type, channel = channel, callId = callId }); }
            catch (Exception e) { Debug.LogWarning($"Call signal '{type}' failed: {e.Message}"); }
        }

        void OnMessage(IMessageReceivedEvent e)
        {
            CallSignal signal;
            try { signal = e.GetAs<CallSignal>(); }
            catch { return; } // not a call message
            if (signal == null || string.IsNullOrEmpty(signal.type)) return;

            // Only accept the channel both sides would compute, so a message can't pull us into another channel.
            var expected = ChannelFor(App.Auth.PlayerId, e.UserId);
            if (signal.channel != expected) return;
            // Older clients only send the channel. New clients also identify the individual call.
            if (signal.type != "ring" && signal.callId != null && signal.callId != _callId) return;

            switch (signal.type)
            {
                case "ring":
                    if (App.Friends.IsBlocked(e.UserId)) return;
                    if (IsCalling(e.UserId) && signal.callId != null && _callId != null)
                    {
                        // Both clients can ring at once and join the same channel.
                        if (string.CompareOrdinal(signal.callId, _callId) < 0)
                        {
                            _callId = signal.callId;
                            _callerId = e.UserId;
                        }
                        return;
                    }
                    if (_startingCall || State != CallState.None)
                    {
                        if (PeerId == e.UserId && signal.callId != null && signal.callId == _callId) return;
                        Signal(e.UserId, expected, signal.callId, "decline").Forget();
                        return;
                    }
                    _channel = expected;
                    _callId = signal.callId;
                    _callerId = e.UserId;
                    SetState(CallState.Incoming, e.UserId);
                    RingTimeoutAsync(_callVersion).Forget();
                    break;

                case "cancel" when State == CallState.Incoming && PeerId == e.UserId:
                    SetState(CallState.None, null);
                    break;

                case "cancel" when State == CallState.Active && PeerId == e.UserId:
                case "hangup" when IsCalling(e.UserId):
                    EndRemoteCallAsync().Forget();
                    break;

                case "decline" when State == CallState.Outgoing && PeerId == e.UserId:
                    App.Report($"{App.Friends.NameOf(e.UserId)} declined the call.");
                    EndRemoteCallAsync().Forget();
                    break;

                case "accept" when State == CallState.Outgoing && PeerId == e.UserId:
                    SetState(CallState.Active, PeerId);
                    break;
            }
        }

        async UniTaskVoid EndRemoteCallAsync()
        {
            var channel = _channel;
            SetState(CallState.None, null);
            await App.Voice.LeaveAsync(channel);
        }

        void OnParticipantsChanged()
        {
            // The peer showing up in the channel also means the call connected.
            if (State == CallState.Outgoing && App.Voice.Channel == _channel
                && App.Voice.Participants.Any(p => p.PlayerId == PeerId))
                SetState(CallState.Active, PeerId);
            StartCallClock();
        }

        void OnVoiceChanged()
        {
            // Leaving the call channel by any route (disconnect button, joining another channel) ends the call.
            if (IsCalling(PeerId) && _joiningVersion != _callVersion && App.Voice.Channel != _channel)
                SetState(CallState.None, null);
            StartCallClock();
        }

        void StartCallClock()
        {
            if (_connectedAt.HasValue || State != CallState.Active || App.Voice.Channel != _channel
                || !App.Voice.IsConnected || !App.Voice.Participants.Any(p => p.PlayerId == PeerId)) return;
            _historyOwner = App.Auth.PlayerId;
            _connectedAt = Time.realtimeSinceStartupAsDouble;
        }

        void SaveCompletedCall(string peer)
        {
            if (!_connectedAt.HasValue) return;
            int seconds = (int)(Time.realtimeSinceStartupAsDouble - _connectedAt.Value);
            _connectedAt = null;
            try
            {
                CallHistory.Record(_historyOwner, peer, _callId ?? Guid.NewGuid().ToString("N"), _callerId, seconds);
                HistoryChanged?.Invoke(peer);
            }
            catch (Exception e) { Debug.LogWarning($"Couldn't save call history: {e.Message}"); }
        }

        async UniTaskVoid RingTimeoutAsync(int version)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(RingTimeoutSeconds), ignoreTimeScale: true);
            if (version != _callVersion) return;
            if (State == CallState.Incoming)
            {
                SetState(CallState.None, null);
                return;
            }
            if (State != CallState.Outgoing) return;
            App.Report($"{App.Friends.NameOf(PeerId)} didn't answer.");
            await HangUpAsync();
        }

        void SetState(CallState state, string peerId)
        {
            if (state == State && peerId == PeerId) return;
            var previousPeer = PeerId;
            if (state is CallState.Incoming or CallState.Outgoing or CallState.None) _callVersion++;
            State = state;
            PeerId = peerId;
            if (state == CallState.None) SaveCompletedCall(previousPeer);
            StateChanged?.Invoke();
        }
    }
}
