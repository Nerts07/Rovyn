using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Ringing card for an incoming private call.
    public class IncomingCallPopup : MonoBehaviour
    {
        public FadePanel panel;
        public Avatar avatar;
        public TMP_Text nameText;
        public Button acceptButton;
        public Button declineButton;

        void Awake()
        {
            acceptButton.onClick.AddListener(() =>
            {
                var peer = App.Calls.PeerId;
                MainScreen.Instance.OpenDirect(peer);
                MainScreen.Instance.ShowVoice();
                UiActions.Run(() => App.Calls.AcceptAsync());
            });
            declineButton.onClick.AddListener(() => UiActions.Run(() => App.Calls.DeclineAsync()));
        }

        void OnEnable()
        {
            App.Calls.StateChanged += Refresh;
            Refresh();
        }

        void OnDisable() => App.Calls.StateChanged -= Refresh;

        void Refresh()
        {
            if (App.Calls.State != CallState.Incoming)
            {
                panel.Hide();
                return;
            }
            var name = App.Friends.NameOf(App.Calls.PeerId);
            avatar.Set(App.Calls.PeerId, name);
            nameText.text = name;
            panel.Show();
        }
    }
}
