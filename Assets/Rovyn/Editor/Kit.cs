using System.Collections.Generic;
using Rovyn.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Avatar = Rovyn.UI.Avatar;

namespace Rovyn.EditorTools
{
    // Small helpers the UI builder uses to create rects, text, images, inputs and layout groups.
    // Every size is in canvas pixels. UiScale turns that into 1 unit = 1 px at 96 DPI at runtime.
    static class Kit
    {
        const string ShapeDir = "Assets/Rovyn/UI/Shapes/";
        const string IconDir = "Assets/Rovyn/UI/Icons/";
        static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Circle => Load(ShapeDir + "circle.svg");
        public static Sprite Rounded => Load(ShapeDir + "rounded.svg");
        public static Sprite RoundedOutline => Load(ShapeDir + "rounded_outline.svg");
        public static Sprite Ring => Load(ShapeDir + "ring.svg");
        public static Sprite SwitchOn => Load(ShapeDir + "switch_on.svg");
        public static Sprite SwitchOff => Load(ShapeDir + "switch_off.svg");
        public static Sprite Icon(string name) => Load(IconDir + name + ".svg");

        public static void ClearCache() => Cache.Clear();

        static Sprite Load(string path)
        {
            if (Cache.TryGetValue(path, out var cached) && cached != null) return cached;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[Rovyn] Sprite not found: {path}");
            Cache[path] = sprite;
            return sprite;
        }

        // ---- Rects ----

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 }; // UI layer
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, float width, float height, float x = 0, float y = 0)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        public static RectTransform Center(RectTransform rt, float width, float height, float x = 0, float y = 0) =>
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), width, height, x, y);

        // Full width strip glued to the top (anchorY 1) or bottom (anchorY 0) edge.
        public static RectTransform Strip(RectTransform rt, float anchorY, float height, float y = 0)
        {
            rt.anchorMin = new Vector2(0, anchorY);
            rt.anchorMax = new Vector2(1, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = new Vector2(0, y);
            return rt;
        }

        // ---- Layout ----

        public static RectOffset Pad(int left, int right, int top, int bottom) => new(left, right, top, bottom);
        public static RectOffset Pad(int all) => new(all, all, all, all);

        public static LayoutElement Elem(GameObject go, float w = -1, float h = -1, float fw = -1, float fh = -1,
            float minW = -1, float minH = -1, bool ignore = false)
        {
            var e = go.GetComponent<LayoutElement>();
            if (e == null) e = go.AddComponent<LayoutElement>();
            if (w >= 0) e.preferredWidth = w;
            if (h >= 0) e.preferredHeight = h;
            if (fw >= 0) e.flexibleWidth = fw;
            if (fh >= 0) e.flexibleHeight = fh;
            if (minW >= 0) e.minWidth = minW;
            if (minH >= 0) e.minHeight = minH;
            if (ignore) e.ignoreLayout = true;
            return e;
        }

        public static HorizontalLayoutGroup HBox(GameObject go, float spacing = 0, RectOffset pad = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool controlW = true, bool controlH = true,
            bool expandW = false, bool expandH = false)
        {
            var g = go.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = pad ?? new RectOffset();
            g.childAlignment = align;
            g.childControlWidth = controlW;
            g.childControlHeight = controlH;
            g.childForceExpandWidth = expandW;
            g.childForceExpandHeight = expandH;
            return g;
        }

        public static VerticalLayoutGroup VBox(GameObject go, float spacing = 0, RectOffset pad = null,
            TextAnchor align = TextAnchor.UpperLeft, bool controlW = true, bool controlH = true,
            bool expandW = true, bool expandH = false)
        {
            var g = go.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = pad ?? new RectOffset();
            g.childAlignment = align;
            g.childControlWidth = controlW;
            g.childControlHeight = controlH;
            g.childForceExpandWidth = expandW;
            g.childForceExpandHeight = expandH;
            return g;
        }

        public static ContentSizeFitter Fit(GameObject go, bool horizontal = false, bool vertical = true)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        // ---- Graphics ----

        // Plain image. A sprite with 9-slice borders keeps a corner radius of `radius` pixels at any size.
        public static Image Img(Transform parent, string name, Color color, Sprite sprite = null, float radius = 0, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null)
            {
                img.sprite = sprite;
                if (sprite.border != Vector4.zero)
                {
                    img.type = Image.Type.Sliced;
                    if (radius > 0f) img.pixelsPerUnitMultiplier = sprite.border.x * 100f / (sprite.pixelsPerUnit * radius);
                }
            }
            return img;
        }

        // Filled rectangle, rounded when radius > 0.
        public static Image Box(Transform parent, string name, Color color, float radius = 0, bool raycast = false) =>
            Img(parent, name, color, radius > 0f ? Rounded : null, radius, raycast);

        public static Image Icon(Transform parent, string name, string icon, float size, Color color)
        {
            var img = Img(parent, name, color, Icon(icon));
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            Elem(img.gameObject, w: size, h: size);
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft,
            bool wrap = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            return t;
        }

        public static TextMeshProUGUI Header(Transform parent, string text) =>
            Label(parent, text + " Header", text, 12, Theme.TextMuted, FontStyles.Bold);

        public static Image Divider(Transform parent, float height = 1) =>
            Box(parent, "Divider", Theme.Divider).With(i => Elem(i.gameObject, h: height));

        public static T With<T>(this T value, System.Action<T> action)
        {
            action(value);
            return value;
        }

        // ---- Buttons ----

        public static Button Btn(GameObject go, Graphic target)
        {
            var b = go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = target;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            return b;
        }

        public static Button TextButton(Transform parent, string name, string text, Color bg, Color fg,
            float height = 36, float width = -1, float radius = 4, float size = 14)
        {
            var img = Box(parent, name, bg, radius, raycast: true);
            var b = Btn(img.gameObject, img);
            var label = Label(img.transform, "Label", text, size, fg, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            Elem(img.gameObject, w: width, h: height);
            img.gameObject.AddComponent<ButtonFx>();
            return b;
        }

        // Square or round icon button. A transparent bg gives a bare icon.
        public static Button IconButton(Transform parent, string name, string icon, float size, float iconSize, Color bg, Color iconColor)
        {
            var img = Img(parent, name, bg, bg.a > 0f ? Circle : null, raycast: true);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            Elem(img.gameObject, w: size, h: size);
            var b = Btn(img.gameObject, img);
            img.gameObject.AddComponent<ButtonFx>();
            var ic = Icon(img.transform, "Icon", icon, iconSize, iconColor);
            Center(ic.rectTransform, iconSize, iconSize);
            return b;
        }

        public static HoverHighlight Hover(GameObject go, Graphic bg, Graphic[] fg = null, GameObject[] showOnHover = null,
            Color? fgNormal = null, Color? fgHover = null, Color? fgSelected = null, Color? hover = null, Color? selected = null)
        {
            var h = go.AddComponent<HoverHighlight>();
            h.background = bg;
            h.normal = Theme.Clear;
            h.hover = hover ?? Theme.Hover;
            h.selected = selected ?? Theme.Selected;
            h.foreground = fg;
            h.foregroundNormal = fgNormal ?? Theme.TextMuted;
            h.foregroundHover = fgHover ?? Theme.TextNormal;
            h.foregroundSelected = fgSelected ?? Color.white;
            h.showOnHover = showOnHover;
            if (bg != null) bg.color = Theme.Clear;
            return h;
        }

        public static BusyButton Busy(Transform parent, string name, string text, Color bg, float height = 44, float width = -1)
        {
            var button = TextButton(parent, name, text, bg, Color.white, height, width, 4, 15);
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            var spinner = Icon(button.transform, "Spinner", "spinner", 22, Color.white);
            Center(spinner.rectTransform, 22, 22);
            spinner.gameObject.AddComponent<Spinner>();
            spinner.gameObject.SetActive(false);

            var busy = button.gameObject.AddComponent<BusyButton>();
            busy.button = button;
            busy.label = label;
            busy.spinner = spinner.gameObject;
            return busy;
        }

        // ---- Inputs ----

        public static TMP_InputField InputBox(Transform parent, string name, string placeholder, bool password = false,
            float height = 40, Color? bg = null, float size = 15)
        {
            var bgImg = Box(parent, name, bg ?? Theme.Input, 4, raycast: true);
            Elem(bgImg.gameObject, h: height);

            var area = Rect("Text Area", bgImg.transform);
            Stretch(area, 12, 0, 12, 0);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Label(area, "Placeholder", placeholder, size, Theme.TextMuted);
            Stretch(ph.rectTransform);
            var text = Label(area, "Text", "", size, Theme.TextNormal);
            Stretch(text.rectTransform);
            text.overflowMode = TextOverflowModes.Overflow;

            var input = bgImg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = bgImg;
            input.transition = Selectable.Transition.None;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.customCaretColor = true;
            input.caretColor = Theme.TextHeader;
            input.selectionColor = Theme.InputSelection;
            return input;
        }

        public static FormField Form(Transform parent, string name, string title, bool password = false, bool required = true)
        {
            var root = Rect(name, parent);
            VBox(root.gameObject, 8);
            var label = Label(root, "Label", title, 12, Theme.Interactive, FontStyles.Bold);
            Elem(label.gameObject, h: 16);
            var input = InputBox(root, "Input", "", password);

            var field = root.gameObject.AddComponent<FormField>();
            field.label = label;
            field.input = input;
            field.title = title;
            field.required = required;
            return field;
        }

        public static Slider SliderBar(Transform parent, string name, float min, float max, bool whole = true)
        {
            var root = Rect(name, parent);
            Elem(root.gameObject, h: 20);

            var bg = Box(root, "Background", Theme.Divider, 4, raycast: true);
            Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 8);
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 8);

            var fillArea = Rect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(0, -4);
            fillArea.offsetMax = new Vector2(0, 4);
            var fill = Box(fillArea, "Fill", Theme.Accent, 4);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.sizeDelta = Vector2.zero;

            var slide = Rect("Handle Slide Area", root);
            Stretch(slide, 8, 0, 8, 0);
            var handle = Img(slide, "Handle", Color.white, Circle, raycast: true);
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.sizeDelta = new Vector2(16, 0);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.value = min;
            return slider;
        }

        public static Toggle Switch(Transform parent, string name)
        {
            var off = Img(parent, name, Color.white, SwitchOff, raycast: true);
            off.rectTransform.sizeDelta = new Vector2(40, 24);
            Elem(off.gameObject, w: 40, h: 24);
            var on = Img(off.transform, "On", Color.white, SwitchOn);
            Stretch(on.rectTransform);

            var toggle = off.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = off;
            toggle.graphic = on;
            toggle.transition = Selectable.Transition.None;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            toggle.isOn = false;
            return toggle;
        }

        public static TMP_Dropdown Dropdown(Transform parent, string name, float height = 40)
        {
            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = name;
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Elem(go, h: height);

            var dd = go.GetComponent<TMP_Dropdown>();
            var bg = go.GetComponent<Image>();
            bg.sprite = Rounded;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = Rounded.border.x * 100f / (Rounded.pixelsPerUnit * 4f);
            bg.color = Theme.Input;
            dd.transition = Selectable.Transition.None;
            dd.navigation = new Navigation { mode = Navigation.Mode.None };

            dd.captionText.color = Theme.TextNormal;
            dd.captionText.fontSize = 15;
            dd.itemText.color = Theme.TextNormal;
            dd.itemText.fontSize = 14;

            var arrow = go.transform.Find("Arrow");
            if (arrow != null && arrow.TryGetComponent<Image>(out var arrowImg))
            {
                arrowImg.sprite = Icon("chevron_down");
                arrowImg.color = Theme.Interactive;
                arrowImg.preserveAspect = true;
            }

            if (dd.template != null)
            {
                if (dd.template.TryGetComponent<Image>(out var templateBg))
                {
                    templateBg.sprite = Rounded;
                    templateBg.type = Image.Type.Sliced;
                    templateBg.pixelsPerUnitMultiplier = Rounded.border.x * 100f / (Rounded.pixelsPerUnit * 4f);
                    templateBg.color = Theme.Popup;
                }
                var item = dd.template.GetComponentInChildren<Toggle>(true);
                if (item != null)
                {
                    ((RectTransform)item.transform).sizeDelta = new Vector2(0, 28);
                    item.transition = Selectable.Transition.ColorTint;
                    var colors = item.colors;
                    colors.normalColor = Theme.Clear;
                    colors.highlightedColor = Theme.Composer;
                    colors.pressedColor = Theme.Composer;
                    colors.selectedColor = Theme.Composer;
                    colors.fadeDuration = 0f;
                    item.colors = colors;
                    if (item.targetGraphic is Image itemBg)
                    {
                        itemBg.sprite = null;
                        itemBg.color = Color.white; // tinted by the colour block above
                    }
                    if (item.graphic != null) item.graphic.color = Theme.Accent;
                }
            }
            return dd;
        }

        // ---- Scrolling ----

        public static ScrollRect Scroll(Transform parent, string name, out RectTransform content,
            float spacing = 0, RectOffset pad = null)
        {
            var root = Rect(name, parent);
            var sr = root.gameObject.AddComponent<ScrollRect>();

            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Drags only register on a raycast target, so the viewport carries an invisible image.
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Theme.Clear;
            catcher.raycastTarget = true;

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            VBox(content.gameObject, spacing, pad);
            Fit(content.gameObject);

            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            return sr;
        }

        // ---- Avatars ----

        public static Avatar MakeAvatar(Transform parent, string name, float size, bool status = false,
            Color? statusRing = null, bool speaking = false)
        {
            var root = Rect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            Elem(root.gameObject, w: size, h: size);

            Image ring = null;
            if (speaking)
            {
                ring = Img(root, "Speaking Ring", Theme.Green, Kit.Ring);
                Stretch(ring.rectTransform, -3, -3, -3, -3);
                ring.enabled = false;
            }

            var bg = Img(root, "Background", Theme.Accent, Circle);
            Stretch(bg.rectTransform);
            var initial = Label(root, "Initial", "?", Mathf.Round(size * 0.45f), Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(initial.rectTransform);

            GameObject statusRoot = null;
            Image dot = null;
            if (status)
            {
                float s = Mathf.Max(10f, Mathf.Round(size * 0.42f));
                var ringImg = Img(root, "Status", statusRing ?? Theme.Sidebar, Circle);
                Place(ringImg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), s, s, 3, -3);
                float inset = s < 14f ? 2f : 3f;
                dot = Img(ringImg.transform, "Dot", Theme.Green, Circle);
                Stretch(dot.rectTransform, inset, inset, inset, inset);
                statusRoot = ringImg.gameObject;
                statusRoot.SetActive(false);
            }

            var avatar = root.gameObject.AddComponent<Avatar>();
            avatar.background = bg;
            avatar.initial = initial;
            avatar.statusRoot = statusRoot;
            avatar.statusDot = dot;
            avatar.speakingRing = ring;
            return avatar;
        }
    }
}
