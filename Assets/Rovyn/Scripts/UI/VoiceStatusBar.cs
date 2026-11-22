using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // "Voice Connected" strip above the user panel while in a voice channel or call.
    public class VoiceStatusBar : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text stateText;
        public TMP_Text channelText;
        public Button openButton;
        public Button disconnectButton;

        void Awake()
        {
            openButton.onClick.AddListener(() => MainScreen.Instance.ShowVoice());
            disconnectButton.onClick.AddListener(Disconnect);
        }

        void OnEnable()
        {
            App.Voice.StateChanged += Refresh;
            App.Calls.StateChanged += Refresh;
            App.Vivox.StateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            App.Voice.StateChanged -= Refresh;
            App.Calls.StateChanged -= Refresh;
            App.Vivox.StateChanged -= Refresh;
        }

        void Refresh()
        {
            root.SetActive(App.Voice.Channel != null);
            if (App.Voice.Channel == null) return;

            if (App.Voice.IsReconnecting || App.Vivox.State == ConnectionState.Recovering) Set("Reconnecting…", Theme.Yellow);
            else if (App.Voice.IsConnecting) Set("Connecting…", Theme.Yellow);
            else if (App.Calls.State == CallState.Outgoing) Set("Calling…", Theme.Yellow);
            else Set("Voice Connected", Theme.Green);

            channelText.text = App.Voice.IsPrivate ? $"Call with {App.Voice.Label}" : App.Voice.Label;
        }

        void Set(string text, Color color)
        {
            stateText.text = text;
            stateText.color = color;
        }

        void Disconnect()
        {
            UiActions.Run(async () =>
            {
                if (App.Calls.State != CallState.None) await App.Calls.HangUpAsync();
                else await App.Voice.LeaveAsync();
            });
        }
    }
}
