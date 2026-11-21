using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Hover, focus, and selected states for rows and icon buttons.
    public class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        public Graphic background;
        public Color normal = new(0f, 0f, 0f, 0f);
        public Color hover = new(0.31f, 0.33f, 0.36f, 0.4f);
        public Color selected = new(0.31f, 0.33f, 0.36f, 0.6f);

        public Graphic[] foreground;
        public Color foregroundNormal = new(0.58f, 0.61f, 0.64f);
        public Color foregroundHover = new(0.86f, 0.87f, 0.88f);
        public Color foregroundSelected = Color.white;

        // Objects only visible while hovered, such as row action buttons.
        public GameObject[] showOnHover;

        bool _hovered;
        bool _selected;
        bool _focused;
        bool _animating;

        public bool Selected
        {
            get => _selected;
            set { _selected = value; Apply(); }
        }

        // How fast colors catch up with their target. Higher is snappier.
        public float speed = 16f;

        Color _bgTarget;
        Color _fgTarget;

        void OnEnable()
        {
            _hovered = false;
            _focused = false;
            Apply();
            Snap();
        }

        public void OnPointerEnter(PointerEventData eventData) { _hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; Apply(); }
        public void OnSelect(BaseEventData eventData) { _focused = true; Apply(); }
        public void OnDeselect(BaseEventData eventData) { _focused = false; Apply(); }

        internal void ApplyPalette()
        {
            normal = UiPresentation.ResolveColor(normal);
            hover = UiPresentation.ResolveColor(hover);
            selected = UiPresentation.ResolveColor(selected);
            foregroundNormal = UiPresentation.ResolveColor(foregroundNormal);
            foregroundHover = UiPresentation.ResolveColor(foregroundHover);
            foregroundSelected = UiPresentation.ResolveColor(foregroundSelected);
            if (!isActiveAndEnabled) return;
            Apply();
            Snap();
        }

        void Update()
        {
            if (!_animating) return;
            float amount = 1f - Mathf.Exp(-Mathf.Max(24f, speed) * Time.unscaledDeltaTime);
            _animating = StepColor(background, _bgTarget, amount);

            if (foreground == null) return;
            foreach (var g in foreground)
                _animating |= StepColor(g, _fgTarget, amount);
        }

        static bool StepColor(Graphic graphic, Color target, float amount)
        {
            if (graphic == null || graphic.color.Equals(target)) return false;
            var next = Color.Lerp(graphic.color, target, amount);
            bool moving = !((Color32)next).Equals((Color32)target);
            graphic.color = moving ? next : target;
            return moving;
        }

        void Snap()
        {
            _animating = false;
            if (background != null) background.color = _bgTarget;
            if (foreground == null) return;
            foreach (var g in foreground)
                if (g != null) g.color = _fgTarget;
        }

        void Apply()
        {
            bool highlighted = _hovered || _focused;
            _bgTarget = _selected || _focused ? selected : _hovered ? hover : normal;
            _fgTarget = _selected ? foregroundSelected : highlighted ? foregroundHover : foregroundNormal;
            _animating = true;

            if (showOnHover != null)
                foreach (var go in showOnHover)
                    if (go != null) go.SetActive(highlighted || _selected);
        }
    }
}
