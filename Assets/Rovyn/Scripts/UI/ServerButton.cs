using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Server shortcuts share the same shape and highlight the current destination.
    public class ServerButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Image icon;
        public Graphic glyph;             // initials text or the home logo
        public TMP_Text initials;
        public RectTransform pill;
        public Sprite circleSprite;
        public Sprite roundedSprite;
        public Color normalColor = new(0.19f, 0.2f, 0.22f);
        public Color activeColor = new(0.43f, 0.36f, 0.99f);
        public Color glyphNormal = new(0.86f, 0.87f, 0.88f);
        public Color glyphActive = Color.white;
        public GameObject badge;
        public TMP_Text badgeText;
        public string tooltip;

        // Assigned (not added) by the owner, so rebinding a pooled button replaces the handler.
        public Action Clicked;

        bool _hovered;
        bool _focused;
        bool _selected;
        bool _animating;

        public bool Selected
        {
            get => _selected;
            set { _selected = value; Apply(); }
        }

        public void SetServer(ServerCatalog.Server server)
        {
            tooltip = server.name;
            if (initials != null) initials.text = Initials(server.name);
            Apply();
        }

        public void SetBadge(int count)
        {
            if (badge == null) return;
            badge.SetActive(count > 0);
            if (count > 0) badgeText.text = count > 99 ? "99+" : count.ToString();
        }

        void Awake()
        {
            normalColor = UiPresentation.ResolveColor(normalColor);
            activeColor = initials == null ? Theme.Brand : UiPresentation.ResolveColor(activeColor);
            glyphNormal = UiPresentation.ResolveColor(glyphNormal);
            UiPresentation.RoundSurface(icon, roundedSprite != null ? roundedSprite : circleSprite, 11f);
            if (pill != null) pill.GetComponent<UnityEngine.UI.Image>().color = Theme.TextLink;
            var button = gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = icon;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.onClick.AddListener(() => Clicked?.Invoke());
        }

        void OnEnable()
        {
            _hovered = false;
            _focused = false;
            Apply();
            MoveAppearance(1f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hovered = true;
            Apply();
            if (Tooltip.Instance != null && !string.IsNullOrEmpty(tooltip))
                Tooltip.Instance.Show(tooltip, (RectTransform)transform);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hovered = false;
            Apply();
            if (Tooltip.Instance != null) Tooltip.Instance.Hide();
        }

        public void OnSelect(BaseEventData e) { _focused = true; Apply(); }
        public void OnDeselect(BaseEventData e) { _focused = false; Apply(); }

        void Apply() => _animating = true;

        void Update()
        {
            if (_animating) MoveAppearance(1f - Mathf.Exp(-28f * Time.unscaledDeltaTime));
        }

        void MoveAppearance(float amount)
        {
            bool active = _hovered || _focused || _selected;
            var target = _selected ? activeColor : active ? Theme.UserPanel : normalColor;
            var next = Color.Lerp(icon.color, target, amount);
            _animating = !((Color32)next).Equals((Color32)target);
            icon.color = _animating ? next : target;
            if (glyph != null) glyph.color = active ? glyphActive : glyphNormal;

            if (pill == null) return;
            float height = _selected ? 26f : active ? 12f : 0f;
            float nextHeight = Mathf.Lerp(pill.sizeDelta.y, height, amount);
            bool moving = Mathf.Abs(nextHeight - height) > 0.5f;
            pill.sizeDelta = new Vector2(pill.sizeDelta.x, moving ? nextHeight : height);
            pill.gameObject.SetActive(pill.sizeDelta.y > 0f);
            _animating |= moving;
        }

        static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = "";
            foreach (var p in parts)
            {
                result += char.ToUpperInvariant(p[0]);
                if (result.Length == 3) break;
            }
            return result;
        }
    }
}
