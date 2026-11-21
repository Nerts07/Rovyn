using TMPro;
using Unity.Services.Vivox;
using UnityEngine;

namespace Rovyn.UI
{
    // A participant listed under the connected voice channel in the server sidebar.
    public class VoiceUserEntry : MonoBehaviour
    {
        public Avatar avatar;
        public TMP_Text nameText;
        public GameObject mutedIcon;

        readonly ParticipantWatcher _watcher = new();

        public void Bind(VivoxParticipant participant)
        {
            _watcher.Watch(participant, Refresh);
            var name = App.Friends.NameOf(participant.PlayerId, participant.DisplayName);
            avatar.Set(participant.PlayerId, name);
            nameText.text = name;
            Refresh();
        }

        void OnEnable() => _watcher.Resume();
        void OnDisable() => _watcher.Pause();

        void Refresh()
        {
            var p = _watcher.Participant;
            if (p == null) return;
            avatar.SetSpeaking(p.SpeechDetected && !p.IsMuted);
            mutedIcon.SetActive(p.IsMuted);
        }
    }
}
