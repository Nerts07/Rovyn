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
        public string type;    // ring, accept, decline, cancel
        public string channel; // Vivox channel both sides join
    }

    // Private 1:1 voice calls. Ringing goes through Friends messages (friends only),
    // the audio itself goes through a Vivox channel named after both player IDs.
    public sealed class CallService
    {
        public event Action StateChanged;

        public CallState State { get; private set; }
        public string PeerId { get; private set; }

        const int RingTimeoutSeconds = 45;

        string _channel;
        int _callVersion; // invalidates stale ring timeouts

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
            if (!App.Friends.IsFriend(peerId)) throw new InvalidOperationException("You can only call friends.");
            if (State != CallState.None) await HangUpAsync();
            // Leave any other voice channel first, otherwise that leave would read as the call ending.
            await App.Voice.LeaveAsync();

            _channel = ChannelFor(App.Auth.PlayerId, peerId);
            SetState(CallState.Outgoing, peerId);
            int version = _callVersion;
            try
            {
                await App.Voice.JoinAsync(_channel, App.Friends.NameOf(peerId), isPrivate: true);
                if (version != _callVersion) return;
                await App.Friends.SendMessageAsync(peerId, new CallSignal { type = "ring", channel = _channel });
            }
            catch
            {
                SetState(CallState.None, null);
                await App.Voice.LeaveAsync();
                throw;
            }
            RingTimeoutAsync(version).Forget();
        }

        public async UniTask AcceptAsync()
        {
            if (State != CallState.Incoming) return;
            var peer = PeerId;
            await App.Voice.LeaveAsync();
            if (State != CallState.Incoming || PeerId != peer) return; // caller hung up meanwhile
            SetState(CallState.Active, peer);
            try
            {
                await App.Voice.JoinAsync(_channel, App.Friends.NameOf(peer), isPrivate: true);
                await App.Friends.SendMessageAsync(peer, new CallSignal { type = "accept", channel = _channel });
            }
            catch
            {
                SetState(CallState.None, null);
                throw;
            }
        }

        public async UniTask DeclineAsync()
        {
            if (State != CallState.Incoming) return;
            var peer = PeerId;
            SetState(CallState.None, null);
            await Signal(peer, "decline");
        }

        public async UniTask HangUpAsync()
        {
            var peer = PeerId;
            var wasOutgoing = State == CallState.Outgoing;
            SetState(CallState.None, null);
            if (wasOutgoing) await Signal(peer, "cancel");
            if (App.Voice.Channel != null && App.Voice.Channel == _channel) await App.Voice.LeaveAsync();
        }

        async UniTask Signal(string peer, string type)
        {
            if (string.IsNullOrEmpty(peer)) return;
            try { await App.Friends.SendMessageAsync(peer, new CallSignal { type = type, channel = _channel }); }
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

            switch (signal.type)
            {
                case "ring":
                    if (App.Friends.IsBlocked(e.UserId)) return;
                    if (State != CallState.None)
                    {
                        // Already in a call: tell the caller we're busy.
                        App.Friends.SendMessageAsync(e.UserId, new CallSignal { type = "decline", channel = expected }).Forget();
                        return;
                    }
                    _channel = expected;
                    SetState(CallState.Incoming, e.UserId);
                    RingTimeoutAsync(_callVersion).Forget();
                    break;

                case "cancel" when State == CallState.Incoming && PeerId == e.UserId:
                    SetState(CallState.None, null);
                    break;

                case "decline" when State == CallState.Outgoing && PeerId == e.UserId:
                    App.Report($"{App.Friends.NameOf(e.UserId)} declined the call.");
                    HangUpAsync().Forget();
                    break;

                case "accept" when State == CallState.Outgoing && PeerId == e.UserId:
                    SetState(CallState.Active, PeerId);
                    break;
            }
        }

        void OnParticipantsChanged()
        {
            // The peer showing up in the channel also means the call connected.
            if (State == CallState.Outgoing && App.Voice.Channel == _channel
                && App.Voice.Participants.Any(p => p.PlayerId == PeerId))
                SetState(CallState.Active, PeerId);
        }

        void OnVoiceChanged()
        {
            // Leaving the call channel by any route (disconnect button, joining another channel) ends the call.
            if ((State == CallState.Outgoing || State == CallState.Active) && App.Voice.Channel != _channel && !App.Voice.IsConnecting)
                SetState(CallState.None, null);
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
            if (state is CallState.Incoming or CallState.Outgoing or CallState.None) _callVersion++;
            State = state;
            PeerId = peerId;
            StateChanged?.Invoke();
        }
    }
}
