using UnityEngine;
using UnityEngine.EventSystems;

namespace Rovyn.UI
{
    [RequireComponent(typeof(RectTransform))]
    internal sealed class ChatHistoryViewport : MonoBehaviour, IScrollHandler
    {
        const float MaxHistoryWidth = 800f;
        const int BottomInset = 12;

        UnityEngine.UI.ScrollRect _scroll;
        UnityEngine.UI.LayoutElement _contentLayout;
        UnityEngine.UI.VerticalLayoutGroup _messages;

        internal void Configure(UnityEngine.UI.ScrollRect scroll)
        {
            _scroll = scroll;
            _scroll.decelerationRate = 0.01f;
            _contentLayout = scroll.content.GetComponent<UnityEngine.UI.LayoutElement>();
            if (_contentLayout == null)
                _contentLayout = scroll.content.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

            _messages = scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            _messages.childAlignment = TextAnchor.LowerLeft;
            _messages.padding = new RectOffset(0, 0, 0, BottomInset);
            OnRectTransformDimensionsChange();
        }

        void OnRectTransformDimensionsChange()
        {
            if (_messages == null) return;

            var viewport = _scroll.viewport.rect;
            _contentLayout.minHeight = viewport.height;
            int rightInset = Mathf.Max(0, Mathf.CeilToInt(viewport.width - MaxHistoryWidth));
            if (_messages.padding.right != rightInset)
                _messages.padding = new RectOffset(0, rightInset, 0, BottomInset);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_scroll.content.rect.height <= _scroll.viewport.rect.height + 0.01f) return;
            float velocity = _scroll.velocity.y - eventData.scrollDelta.y * _scroll.scrollSensitivity * 6f;
            _scroll.velocity = new Vector2(0f, Mathf.Clamp(velocity, -2200f, 2200f));
        }
    }
}
