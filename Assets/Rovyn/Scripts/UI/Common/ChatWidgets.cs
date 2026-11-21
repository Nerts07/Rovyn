using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    internal static class ChatWidgets
    {
        internal static RectTransform Rect(Transform parent, string name)
        {
            var item = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            var rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        internal static void Stretch(RectTransform rect, Vector2 lower, Vector2 upper)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = lower;
            rect.offsetMax = upper;
        }

        internal static UnityEngine.UI.LayoutElement Size(RectTransform rect, float width, float height)
        {
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
            return layout;
        }

        internal static TextMeshProUGUI Label(RectTransform rect, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = false;
            label.raycastTarget = false;
            return label;
        }

        internal static UnityEngine.UI.Button Button(Transform parent, TMP_FontAsset font, Sprite shape, string label,
            float width, float height, Action clicked, out TextMeshProUGUI caption)
        {
            var rect = Rect(parent, label);
            Size(rect, width, height);
            var bg = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            UiPresentation.RoundSurface(bg, shape, 6f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = bg;
            Tint(button, Theme.UserPanel);
            if (clicked != null) button.onClick.AddListener(() => clicked());
            var content = Rect(rect, "Label");
            Stretch(content, new Vector2(5, 0), new Vector2(-5, 0));
            caption = Label(content, font, 13, Theme.TextNormal, TextAlignmentOptions.Center);
            caption.text = label;
            return button;
        }

        internal static void Tint(UnityEngine.UI.Button button, Color color)
        {
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.disabledColor = Theme.Sidebar;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
        }
    }
}
