using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Top banner for connection problems with Vivox or the Friends event stream.
    public class ConnectionBanner : MonoBehaviour
    {
        public GameObject root;
        public Image background;
        public TMP_Text message;
        public Button reconnectButton;

        void Awake() => reconnectButton.onClick.AddListener(() => App.Vivox.ConnectAsync().Forget());

        void OnEnable()
        {
            App.Vivox.StateChanged += Refresh;
            App.Friends.ConnectionChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            App.Vivox.StateChanged -= Refresh;
            App.Friends.ConnectionChanged -= Refresh;
        }

        void Refresh()
        {
            switch (App.Vivox.State)
            {
                case ConnectionState.Failed:
                    Show(App.Vivox.LastError ?? "Disconnected from chat and voice.", Theme.DangerButton, true);
                    return;
                case ConnectionState.Connecting:
                    Show("Connecting to chat and voice…", Theme.Accent, false);
                    return;
                case ConnectionState.Recovering:
                    Show("Connection lost. Reconnecting to chat and voice…", Theme.Hex(0xB97A0F), false);
                    return;
            }

            if (App.Friends.IsReady && !App.Friends.IsConnected)
            {
                Show("Live friend updates paused. Reconnecting…", Theme.Hex(0xB97A0F), false);
                return;
            }
            root.SetActive(false);
        }

        void Show(string text, Color color, bool canReconnect)
        {
            root.SetActive(true);
            message.text = text;
            background.color = color;
            reconnectButton.gameObject.SetActive(canReconnect);
        }
    }
}
