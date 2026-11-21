using TMPro;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Per-user voice controls. Both settings only change what you hear.
    public class ParticipantMenu : MonoBehaviour
    {
        public FadePanel panel;
        public Button backdrop;
        public Avatar avatar;
        public TMP_Text nameText;
        public Slider volume;        // 0-200 %, maps to Vivox -50..50
        public TMP_Text volumeValue;
        public Toggle muteToggle;
        public Button profileButton;

        VivoxParticipant _participant;
        bool _binding;

        void Awake()
        {
            backdrop.onClick.AddListener(Close);
            volume.onValueChanged.AddListener(OnVolume);
            muteToggle.onValueChanged.AddListener(OnMute);
            profileButton.onClick.AddListener(() =>
            {
                var id = _participant?.PlayerId;
                Close();
                if (id != null) MainScreen.Instance.ShowProfile(id);
            });
        }

        public void Open(VivoxParticipant participant)
        {
            _participant = participant;
            _binding = true;
            var name = App.Friends.NameOf(participant.PlayerId, participant.DisplayName);
            avatar.Set(participant.PlayerId, name);
            nameText.text = name;
            volume.value = (participant.LocalVolume + 50) * 2;
            volumeValue.text = $"{volume.value:0}%";
            muteToggle.isOn = participant.IsMuted;
            _binding = false;
            panel.Show();
        }

        public void Close()
        {
            _participant = null;
            panel.Hide();
        }

        void OnVolume(float percent)
        {
            volumeValue.text = $"{percent:0}%";
            if (_binding || _participant == null) return;
            App.Voice.SetUserVolume(_participant, Mathf.RoundToInt(percent / 2f) - 50);
        }

        void OnMute(bool muted)
        {
            if (_binding || _participant == null) return;
            App.Voice.SetUserMuted(_participant, muted);
        }
    }
}
