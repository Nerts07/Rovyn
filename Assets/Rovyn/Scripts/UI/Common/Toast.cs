using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Rovyn.UI
{
    // Shows App.Report messages for a few seconds at the bottom of the window.
    public class Toast : MonoBehaviour
    {
        public FadePanel panel;
        public TMP_Text label;
        public float seconds = 4f;

        int _version;

        void Awake() => App.Notice += Show;
        void OnDestroy() => App.Notice -= Show;

        void Show(string message)
        {
            label.text = message;
            panel.Show();
            HideLaterAsync(++_version).Forget();
        }

        async UniTaskVoid HideLaterAsync(int version)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(seconds), ignoreTimeScale: true);
            if (this != null && version == _version) panel.Hide();
        }
    }
}
