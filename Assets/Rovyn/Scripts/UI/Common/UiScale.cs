using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Desktop-style scaling: 1 UI unit = 1 pixel at 96 DPI, times the user's zoom setting.
    // Layouts stretch with the window through anchors, they are not scaled to a reference size.
    [RequireComponent(typeof(CanvasScaler))]
    public class UiScale : MonoBehaviour
    {
        const string ZoomPref = "rovyn.ui.zoom";

        public static float Zoom
        {
            get => PlayerPrefs.GetFloat(ZoomPref, 1f);
            set
            {
                PlayerPrefs.SetFloat(ZoomPref, Mathf.Clamp(value, 0.5f, 2f));
                foreach (var scale in FindObjectsByType<UiScale>(FindObjectsSortMode.None)) scale.Apply();
            }
        }

        void Awake() => Apply();

        void Apply()
        {
            float dpiScale = Screen.dpi > 0f ? Mathf.Clamp(Screen.dpi / 96f, 1f, 3f) : 1f;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = dpiScale * Zoom;
        }
    }
}
