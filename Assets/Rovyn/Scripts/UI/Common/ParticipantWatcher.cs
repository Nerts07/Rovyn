using System;
using Unity.Services.Vivox;

namespace Rovyn.UI
{
    // Subscribes a callback to a Vivox participant's speaking and mute events,
    // and unsubscribes when the item is hidden or rebound to someone else.
    public sealed class ParticipantWatcher
    {
        public VivoxParticipant Participant { get; private set; }

        Action _callback;
        bool _hooked;

        public void Watch(VivoxParticipant participant, Action callback)
        {
            Pause();
            Participant = participant;
            _callback = callback;
            Resume();
        }

        public void Resume()
        {
            if (_hooked || Participant == null) return;
            Participant.ParticipantSpeechDetected += OnChanged;
            Participant.ParticipantMuteStateChanged += OnChanged;
            _hooked = true;
            _callback?.Invoke();
        }

        public void Pause()
        {
            if (!_hooked || Participant == null) return;
            Participant.ParticipantSpeechDetected -= OnChanged;
            Participant.ParticipantMuteStateChanged -= OnChanged;
            _hooked = false;
        }

        void OnChanged() => _callback?.Invoke();
    }
}
