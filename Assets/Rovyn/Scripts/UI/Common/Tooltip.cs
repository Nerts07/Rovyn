using TMPro;
using UnityEngine;

namespace Rovyn.UI
{
    // One shared tooltip, placed to the right of the hovered element (server names on the rail).
    public class Tooltip : MonoBehaviour
    {
        public static Tooltip Instance { get; private set; }

        public RectTransform box;
        public TMP_Text label;

        void Awake()
        {
            Instance = this;
            box.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(string text, RectTransform target)
        {
            label.text = text;
            box.gameObject.SetActive(true);
            box.SetAsLastSibling();

            // Right edge of the target, vertically centered, in the tooltip parent's space.
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var anchor = (corners[2] + corners[3]) * 0.5f;
            box.position = anchor;
            box.anchoredPosition += new Vector2(12f, 0f);
        }

        public void Hide() => box.gameObject.SetActive(false);
    }
}
