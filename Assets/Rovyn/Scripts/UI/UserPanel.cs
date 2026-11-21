using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Bottom-left panel: your avatar, name and status, plus mic, headphones and settings buttons.
    public class UserPanel : MonoBehaviour
    {
        public Button profileButton;
        public Avatar avatar;
        public TMP_Text nameText;
        public TMP_Text statusText;
        public Button micButton;
        public Image micIcon;
        public Sprite micOn;
        public Sprite micOff;
        public Button deafenButton;
        public Image deafenIcon;
        public Sprite headphonesOn;
        public Sprite headphonesOff;
        public Button settingsButton;

        void Awake()
        {
            profileButton.onClick.AddListener(() => MainScreen.Instance.ShowProfile(App.Auth.PlayerId));
            micButton.onClick.AddListener(() => App.Voice.SetMuted(!(App.Voice.IsMuted || App.Voice.IsDeafened)));
            deafenButton.onClick.AddListener(() => App.Voice.SetDeafened(!App.Voice.IsDeafened));
            settingsButton.onClick.AddListener(() => MainScreen.Instance.ShowSettings());
        }

        void OnEnable()
        {
            App.Auth.NameChanged += Refresh;
            App.Friends.PresenceChanged += OnPresence;
            App.Voice.StateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            App.Auth.NameChanged -= Refresh;
            App.Friends.PresenceChanged -= OnPresence;
            App.Voice.StateChanged -= Refresh;
        }

        void OnPresence(string id)
        {
            if (id == App.Auth.PlayerId) Refresh();
        }

        void Refresh()
        {
            var id = App.Auth.PlayerId;
            var name = App.Auth.DisplayName;
            avatar.Set(id, name);
            avatar.SetStatus(App.Friends.MyStatus);
            nameText.text = name;
            var custom = App.Friends.MyActivity?.status;
            statusText.text = string.IsNullOrEmpty(custom) ? Theme.StatusLabel(App.Friends.MyStatus) : custom;

            bool micMuted = App.Voice.IsMuted || App.Voice.IsDeafened;
            micIcon.sprite = micMuted ? micOff : micOn;
            micIcon.color = micMuted ? Theme.Red : Theme.Interactive;
            deafenIcon.sprite = App.Voice.IsDeafened ? headphonesOff : headphonesOn;
            deafenIcon.color = App.Voice.IsDeafened ? Theme.Red : Theme.Interactive;
        }
    }
}
