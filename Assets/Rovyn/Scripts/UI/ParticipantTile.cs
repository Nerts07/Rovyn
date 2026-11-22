using TMPro;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Large tile in the voice view. Green outline while the person is speaking.
    public class ParticipantTile : MonoBehaviour
    {
        public Button button;
        public Image background;
        public Avatar avatar;
        public TMP_Text nameText;
        public GameObject mutedIcon;
        public Image speakingOutline;
        public CanvasGroup group;

        readonly ParticipantWatcher _watcher = new();

        void Awake() => button.onClick.AddListener(OnClick);
        void OnEnable() => _watcher.Resume();
        void OnDisable() => _watcher.Pause();

        public void Bind(VivoxParticipant participant)
        {
            _watcher.Watch(participant, Refresh);
            var name = App.Friends.NameOf(participant.PlayerId, participant.DisplayName);
            avatar.Set(participant.PlayerId, name);
            nameText.text = participant.IsSelf ? $"{name} (you)" : name;
            background.color = Color.Lerp(Theme.Rail, Theme.AvatarColor(participant.PlayerId), 0.25f);
            group.alpha = 1f;
            Refresh();
        }

        // Someone we're calling who hasn't joined yet.
        public void BindRinging(string playerId)
        {
            _watcher.Watch(null, null);
            var name = App.Friends.NameOf(playerId);
            avatar.Set(playerId, name);
            nameText.text = $"{name} · Ringing…";
            background.color = Theme.Rail;
            mutedIcon.SetActive(false);
            speakingOutline.enabled = false;
            group.alpha = 0.6f;
        }

        internal void SetCompact(bool compact, float height)
        {
            var icon = (RectTransform)avatar.transform;
            float size = compact ? Mathf.Clamp(height - 27f, 0f, 54f) : 80f;
            icon.gameObject.SetActive(size >= 18f);
            icon.sizeDelta = new Vector2(size, size);
            icon.anchoredPosition = new Vector2(0f, compact ? 9f : 0f);
            avatar.initial.fontSize = Mathf.Round(size * 0.45f);

            var caption = nameText.rectTransform;
            caption.anchorMax = new Vector2(1f, 0f);
            caption.pivot = Vector2.zero;
            caption.offsetMin = new Vector2(compact ? 8f : 14f, compact ? 2f : 10f);
            caption.offsetMax = new Vector2(-32f, compact ? 22f : 34f);
            nameText.overflowMode = TextOverflowModes.Ellipsis;
        }

        void Refresh()
        {
            var p = _watcher.Participant;
            if (p == null) return;
            bool speaking = p.SpeechDetected && !p.IsMuted;
            speakingOutline.enabled = speaking;
            avatar.SetSpeaking(speaking);
            mutedIcon.SetActive(p.IsMuted);
        }

        void OnClick()
        {
            var p = _watcher.Participant;
            if (p == null) return;
            if (p.IsSelf) MainScreen.Instance.ShowProfile(p.PlayerId);
            else MainScreen.Instance.participantMenu.Open(p);
        }
    }
}
