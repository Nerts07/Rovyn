using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rovyn.UI
{
    internal sealed class MessageActions : MonoBehaviour
    {
        const int ButtonSize = 27;
        const int Spacing = 3;
        const int Padding = 5;
        const int Height = 34;

        readonly TMP_Text[] _reactionLabels = new TMP_Text[ChatPage.QuickReactionCount];
        readonly UnityEngine.UI.Button[] _reactionButtons = new UnityEngine.UI.Button[ChatPage.QuickReactionCount];
        RectTransform _rect;
        MessageItem _message;
        ChatPage _page;
        string[] _recent;

        internal static MessageActions Create(MessageItem message, ChatPage page, Sprite shape)
        {
            var rect = ChatWidgets.Rect(message.transform, "Message Actions");
            rect.gameObject.SetActive(false);
            ChatWidgets.Size(rect, -1, -1).ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            int buttons = ChatPage.QuickReactionCount + 2;
            rect.sizeDelta = new Vector2(buttons * (ButtonSize + Spacing) + 2 * Padding + 1, Height);

            var actions = rect.gameObject.AddComponent<MessageActions>();
            actions._rect = rect;
            actions._message = message;
            actions._page = page;
            actions.Build(shape);
            return actions;
        }

        void Build(Sprite shape)
        {
            var background = gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = Theme.Composer;
            UiPresentation.RoundSurface(background, shape, 9);
            var row = gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.padding = new RectOffset(Padding, Padding, 0, 0);
            row.spacing = Spacing;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            for (int i = 0; i < _reactionButtons.Length; i++)
            {
                int index = i;
                _reactionButtons[i] = ActionButton(shape, "Quick reaction",
                    () => _page.ToggleReaction(_message, _recent[index]), out var label);
                label.fontSize = 19;
                _reactionLabels[i] = label;
            }

            var divider = ChatWidgets.Rect(_rect, "Divider");
            ChatWidgets.Size(divider, 1, 17);
            var line = divider.gameObject.AddComponent<UnityEngine.UI.Image>();
            line.color = Theme.Divider;
            line.raycastTarget = false;
            ActionButton(shape, "Choose a reaction", () => _page.PickReaction(_message), out var picker);
            picker.fontSize = 18;
            EmojiCatalog.SetText(picker, "🙂");

            var reply = ActionButton(shape, "Reply", () => _page.ReplyTo(_message), out var caption);
            caption.gameObject.SetActive(false);
            var icon = ChatWidgets.Rect(reply.transform, "Reply Arrow");
            ChatWidgets.Stretch(icon, new Vector2(4, 4), new Vector2(-4, -4));
            var glyph = icon.gameObject.AddComponent<ReplyGlyph>();
            glyph.color = Theme.Interactive;
            glyph.raycastTarget = false;
        }

        UnityEngine.UI.Button ActionButton(Sprite shape, string name, Action click, out TextMeshProUGUI label)
        {
            var button = ChatWidgets.Button(_rect, _message.body.font, shape, name, ButtonSize, ButtonSize,
                click, out label);
            var colors = button.colors;
            colors.normalColor = Theme.Clear;
            colors.highlightedColor = Theme.Divider;
            colors.selectedColor = Theme.Accent;
            button.colors = colors;
            ChatWidgets.Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        internal void Show()
        {
            _recent = _page.QuickReactions();
            for (int i = 0; i < _reactionButtons.Length; i++)
            {
                bool available = i < _recent.Length;
                _reactionButtons[i].gameObject.SetActive(available);
                if (!available) continue;
                _reactionButtons[i].name = $"React with {_recent[i]}";
                EmojiCatalog.SetText(_reactionLabels[i], _recent[i]);
            }
            Position();
            gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            var events = EventSystem.current;
            var focused = events != null ? events.currentSelectedGameObject : null;
            bool focusedInside = focused != null && focused.transform.IsChildOf(_message.transform);
            if (!_page.composer.interactable || (!_message.PointerInside && !focusedInside))
            {
                gameObject.SetActive(false);
                return;
            }
            Position();
        }

        void Position()
        {
            var viewport = _page.scroll.viewport;
            float top = viewport.InverseTransformPoint(_message.transform.TransformPoint(
                new Vector3(0, ((RectTransform)_message.transform).rect.yMax, 0))).y;
            _rect.anchoredPosition = new Vector2(-8, Mathf.Min(22, viewport.rect.yMax - top - 1));
        }
    }
}
