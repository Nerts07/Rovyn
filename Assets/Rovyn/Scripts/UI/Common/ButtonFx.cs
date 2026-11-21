using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Subtle scale feedback for hover and press.
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.05f;
        public float pressScale = 0.94f;
        public float speed = 18f;

        bool _hovered;
        bool _pressed;
        Selectable _selectable;

        void Awake() => _selectable = GetComponent<Selectable>();

        void OnEnable()
        {
            _hovered = _pressed = false;
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData e) => _hovered = true;
        public void OnPointerExit(PointerEventData e) { _hovered = false; _pressed = false; }
        public void OnPointerDown(PointerEventData e) => _pressed = e.button == PointerEventData.InputButton.Left;
        public void OnPointerUp(PointerEventData e) => _pressed = false;

        void Update()
        {
            bool active = _selectable == null || _selectable.IsInteractable();
            float target = !active ? 1f : _pressed ? Mathf.Max(0.97f, pressScale) : _hovered ? Mathf.Min(1.015f, hoverScale) : 1f;
            float current = transform.localScale.x;
            if (Mathf.Approximately(current, target)) return;

            float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            if (Mathf.Abs(next - target) < 0.001f) next = target;
            transform.localScale = new Vector3(next, next, 1f);
        }
    }
}
