using System;
using TMPro;
using UnityEngine;

namespace Rovyn.UI
{
    internal sealed class VoiceProcessingView : MonoBehaviour
    {
        readonly struct Switch
        {
            readonly UnityEngine.UI.Toggle _input;
            readonly UnityEngine.UI.Image _track;
            readonly RectTransform _knob;

            internal Switch(UnityEngine.UI.Toggle input, UnityEngine.UI.Image track, RectTransform knob)
            {
                _input = input;
                _track = track;
                _knob = knob;
            }

            internal void Bind(bool enabled)
            {
                _input.SetIsOnWithoutNotify(enabled);
                _track.color = enabled ? Theme.Accent : Theme.Input;
                _knob.anchorMin = _knob.anchorMax = new Vector2(enabled ? 1 : 0, 0.5f);
                _knob.anchoredPosition = new Vector2(enabled ? -12 : 12, 0);
            }
        }

        Switch _automatic;
        Switch _noise;
        Switch _echo;
        UnityEngine.UI.Slider _threshold;
        TMP_Text _thresholdValue;
        TMP_Text _error;

        internal static void Install(RectTransform page, UnityEngine.UI.Slider sliderTemplate, TMP_FontAsset font, Sprite shape)
        {
            var scrollContent = MakePageScrollable(page, shape);
            var card = ChatWidgets.Rect(scrollContent, "Microphone Processing");
            var micTest = scrollContent.Find("Mic Test Title");
            if (micTest != null) card.SetSiblingIndex(micTest.GetSiblingIndex());
            card.gameObject.SetActive(false);
            var view = card.gameObject.AddComponent<VoiceProcessingView>();
            view.Build(card, sliderTemplate, font, shape);
            card.gameObject.SetActive(true);
        }

        void OnEnable()
        {
            App.Voice.Capture.Changed += Bind;
            Bind();
        }

        void OnDisable() => App.Voice.Capture.Changed -= Bind;

        void Bind()
        {
            var settings = App.Voice.Capture;
            _automatic.Bind(settings.AutomaticSensitivity);
            _noise.Bind(settings.NoiseSuppression);
            _echo.Bind(settings.EchoCancellation);
            _threshold.SetValueWithoutNotify(settings.ActivationThreshold);
            _threshold.interactable = !settings.AutomaticSensitivity;
            _thresholdValue.text = settings.AutomaticSensitivity ? "Auto" : $"{settings.ActivationThreshold}";
            _error.text = settings.Error ?? "";
            _error.gameObject.SetActive(settings.Error != null);
        }

        void Build(RectTransform parent, UnityEngine.UI.Slider sliderTemplate, TMP_FontAsset font, Sprite shape)
        {
            var background = parent.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = Theme.Sidebar;
            background.raycastTarget = false;
            UiPresentation.RoundSurface(background, shape, 10);
            var layout = Column(parent, 12);
            layout.padding = new RectOffset(16, 16, 16, 16);

            var title = ChatWidgets.Label(ChatWidgets.Rect(parent, "Title"), font, 16, Theme.TextHeader);
            title.fontStyle = FontStyles.Bold;
            title.text = "Microphone processing";
            var settings = App.Voice.Capture;
            _automatic = AddSwitch(parent, "Automatic sensitivity", "Adjusts the voice threshold as you speak.",
                settings.SetAutomaticSensitivity, font, shape);

            var manual = Row(parent, "Voice activation threshold");
            _threshold = UnityEngine.Object.Instantiate(sliderTemplate, manual, false);
            _threshold.name = "Voice activation threshold";
            _threshold.onValueChanged = new UnityEngine.UI.Slider.SliderEvent();
            _threshold.minValue = 0;
            _threshold.maxValue = VoiceCaptureSettings.MaxThreshold;
            _threshold.wholeNumbers = true;
            _threshold.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            _threshold.onValueChanged.AddListener(value => settings.SetActivationThreshold(Mathf.RoundToInt(value)));
            var sliderSize = _threshold.GetComponent<UnityEngine.UI.LayoutElement>();
            sliderSize.minWidth = 0;
            sliderSize.preferredWidth = 0;
            sliderSize.flexibleWidth = 1;
            _thresholdValue = ChatWidgets.Label(ChatWidgets.Rect(manual, "Threshold Value"), font, 13,
                Theme.TextMuted, TextAlignmentOptions.Center);
            ChatWidgets.Size(_thresholdValue.rectTransform, 42, 22);
            Hint(parent, "A higher threshold requires a louder voice. Use Mic Test to check the setting.", font);

            _noise = AddSwitch(parent, "Noise suppression", "Reduces steady background noise such as fans.",
                settings.SetNoiseSuppression, font, shape);
            _echo = AddSwitch(parent, "Echo cancellation", "Reduces speaker audio picked up by your microphone.",
                settings.SetEchoCancellation, font, shape);
            _error = Hint(parent, "", font);
            _error.color = Theme.ErrorText;
        }

        static Switch AddSwitch(RectTransform parent, string name, string help, Action<bool> changed,
            TMP_FontAsset font, Sprite shape)
        {
            var row = Row(parent, name);
            var text = ChatWidgets.Rect(row, "Description");
            var textSize = ChatWidgets.Size(text, 0, -1);
            textSize.minWidth = 0;
            textSize.flexibleWidth = 1;
            Column(text, 3);
            var label = ChatWidgets.Label(ChatWidgets.Rect(text, "Label"), font, 15, Theme.TextHeader);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.text = name;
            Hint(text, help, font);

            var control = ChatWidgets.Rect(row, name + " Switch");
            ChatWidgets.Size(control, 44, 24);
            var track = control.gameObject.AddComponent<UnityEngine.UI.Image>();
            UiPresentation.RoundSurface(track, shape, 12);
            var knob = ChatWidgets.Rect(control, "Knob");
            knob.sizeDelta = new Vector2(16, 16);
            var dot = knob.gameObject.AddComponent<UnityEngine.UI.Image>();
            dot.color = Theme.TextHeader;
            dot.raycastTarget = false;
            UiPresentation.RoundSurface(dot, shape, 8);
            var toggle = control.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = track;
            toggle.onValueChanged.AddListener(value => changed(value));
            return new Switch(toggle, track, knob);
        }

        static TextMeshProUGUI Hint(RectTransform parent, string text, TMP_FontAsset font)
        {
            var label = ChatWidgets.Label(ChatWidgets.Rect(parent, "Hint"), font, 13, Theme.TextMuted);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.text = text;
            return label;
        }

        static UnityEngine.UI.VerticalLayoutGroup Column(RectTransform parent, int spacing)
        {
            var column = parent.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            column.spacing = spacing;
            return column;
        }

        static RectTransform Row(RectTransform parent, string name)
        {
            var rect = ChatWidgets.Rect(parent, name);
            var row = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            row.spacing = 12;
            return rect;
        }

        static RectTransform MakePageScrollable(RectTransform page, Sprite shape)
        {
            var children = new Transform[page.childCount];
            for (int i = 0; i < children.Length; i++) children[i] = page.GetChild(i);
            page.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().enabled = false;

            var viewport = ChatWidgets.Rect(page, "Voice Viewport");
            ChatWidgets.Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Theme.Clear;
            var content = ChatWidgets.Rect(viewport, "Voice Content");
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            Column(content, 20).padding = new RectOffset(28, 84, 44, 32);
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            foreach (var child in children) child.SetParent(content, false);

            var scroll = page.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            UiPresentation.AddScrollbars(page.gameObject, shape);
            return content;
        }
    }
}
