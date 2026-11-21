using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Rovyn.UI
{
    internal static class UiPresentation
    {
        // The existing scene and prefabs store the builder's old colors as literal values.
        // Apply the new palette to instances so saved assets and inspector wiring can be kept.
        static readonly Dictionary<Color32, Color> SavedColors = new()
        {
            [Theme.Hex(0x1E1F22)] = Theme.Rail,
            [Theme.Hex(0x2B2D31)] = Theme.Sidebar,
            [Theme.Hex(0x313338)] = Theme.Main,
            [Theme.Hex(0x232428)] = Theme.UserPanel,
            [Theme.Hex(0x383A40)] = Theme.Composer,
            [Theme.Hex(0x111214)] = Theme.Popup,
            [Theme.Hex(0x3F4147)] = Theme.Divider,
            [Theme.Hex(0x2E3035)] = Theme.MessageHover,
            [new Color(0.31f, 0.33f, 0.36f, 0.4f)] = Theme.Hover,
            [new Color(0.31f, 0.33f, 0.36f, 0.6f)] = Theme.Selected,
            [Theme.Hex(0xF2F3F5)] = Theme.TextHeader,
            [Theme.Hex(0xDBDEE1)] = Theme.TextNormal,
            [Theme.Hex(0x949BA4)] = Theme.TextMuted,
            [Theme.Hex(0x00A8FC)] = Theme.TextLink,
            [Theme.Hex(0xB5BAC1)] = Theme.Interactive,
            [Theme.Hex(0x6D5DFC)] = Theme.Accent,
            [Theme.Hex(0x23A55A)] = Theme.Green,
            [Theme.Hex(0x248046)] = Theme.GreenButton,
            [Theme.Hex(0xF0B232)] = Theme.Yellow,
            [Theme.Hex(0xF23F43)] = Theme.DangerButton,
            [Theme.Hex(0xDA373C)] = Theme.DangerButton,
            [Theme.Hex(0x80848E)] = Theme.Grey,
            [Theme.Hex(0xFA777C)] = Theme.ErrorText,
        };

        internal static Color ResolveColor(Color color) =>
            SavedColors.TryGetValue(color, out var themed) ? themed : color;

        internal static void ApplyPalette(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.color = ResolveColor(graphic.color);

            foreach (var highlight in root.GetComponentsInChildren<HoverHighlight>(true))
                highlight.ApplyPalette();

            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                input.caretColor = Theme.TextHeader;
                input.selectionColor = Theme.InputSelection;
            }
        }

        internal static UnityEngine.UI.Button AddFieldAction(TMP_InputField field, string label, float width, bool primary)
        {
            var action = new GameObject(label, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button)) { layer = field.gameObject.layer };
            var rect = (RectTransform)action.transform;
            rect.SetParent(field.transform, false);
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, 30f);
            rect.anchoredPosition = new Vector2(-6f, 0f);

            var image = action.GetComponent<UnityEngine.UI.Image>();
            var fieldImage = field.targetGraphic as UnityEngine.UI.Image;
            if (fieldImage != null) RoundSurface(image, fieldImage.sprite, 6f);

            var button = action.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = primary ? Theme.Accent : Theme.UserPanel;
            colors.highlightedColor = primary ? Theme.AccentHover : Theme.Divider;
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? Theme.AccentPressed : Theme.Rail;
            colors.disabledColor = Theme.Sidebar;
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var caption = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI))
                { layer = action.layer };
            caption.transform.SetParent(rect, false);
            var text = caption.GetComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = field.textComponent.font;
            text.fontSize = 13;
            text.fontStyle = FontStyles.Bold;
            text.color = Theme.TextHeader;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = label;
            return button;
        }

        internal static void AddSearchClear(TMP_InputField search)
        {
            const float width = 48f;
            var image = (UnityEngine.UI.Image)search.targetGraphic;
            RoundSurface(image, image.sprite, 8f);
            var clear = AddFieldAction(search, "Clear", width, false);
            var viewport = search.textViewport;
            float right = viewport.offsetMax.x;

            void UpdateClear(string query)
            {
                bool visible = query.Length > 0;
                clear.gameObject.SetActive(visible);
                viewport.offsetMax = new Vector2(visible ? -width - 12f : right, viewport.offsetMax.y);
            }

            clear.onClick.AddListener(() =>
            {
                search.text = "";
                search.Select();
                search.ActivateInputField();
            });
            search.onValueChanged.AddListener(UpdateClear);
            UpdateClear(search.text);
        }

        internal static void RoundSurface(UnityEngine.UI.Image image, Sprite shape, float radius)
        {
            if (shape == null) return;
            image.sprite = shape;
            if (shape.border == Vector4.zero) return;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = shape.border.x * 100f / (shape.pixelsPerUnit * radius);
        }

        internal static void AddScrollbars(GameObject root, Sprite shape)
        {
            foreach (var scroll in root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
            {
                if (!scroll.vertical || scroll.verticalScrollbar != null || scroll.viewport == null) continue;

                var track = new GameObject("Scrollbar", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Scrollbar)) { layer = scroll.gameObject.layer };
                var rect = (RectTransform)track.transform;
                rect.SetParent(scroll.transform, false);
                rect.anchorMin = new Vector2(1, 0);
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-12, 6);
                rect.offsetMax = new Vector2(-2, -6);
                var background = track.GetComponent<UnityEngine.UI.Image>();
                background.color = Theme.Rail;
                RoundSurface(background, shape, 5f);

                var thumb = new GameObject("Thumb", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                    { layer = track.layer };
                var handle = (RectTransform)thumb.transform;
                handle.SetParent(rect, false);
                handle.offsetMin = handle.offsetMax = Vector2.zero;
                var image = thumb.GetComponent<UnityEngine.UI.Image>();
                RoundSurface(image, shape, 5f);

                var bar = track.GetComponent<UnityEngine.UI.Scrollbar>();
                bar.targetGraphic = image;
                bar.handleRect = handle;
                bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
                var colors = bar.colors;
                colors.normalColor = Theme.ScrollThumb;
                colors.highlightedColor = Theme.Grey;
                colors.selectedColor = Theme.TextLink;
                colors.pressedColor = Theme.Interactive;
                colors.fadeDuration = 0.12f;
                bar.colors = colors;

                var offset = scroll.viewport.offsetMax;
                scroll.viewport.offsetMax = new Vector2(offset.x - 12f, offset.y);
                track.SetActive(false);
                scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
                scroll.verticalScrollbar = bar;
                scroll.scrollSensitivity = 44f;
            }
        }
    }
}
