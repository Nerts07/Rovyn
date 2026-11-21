using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Consecutive messages from the same author share a header.
    public class MessageItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
    {
        public HorizontalLayoutGroup layout;
        public GameObject header;          // name + timestamp line
        public Avatar avatar;
        public TMP_Text author;
        public TMP_Text timestamp;
        public TMP_Text body;
        public TMP_Text hoverTime;         // short time shown left of compact messages on hover
        public Button authorButton;

        public string MessageId { get; private set; }
        public string SenderId { get; private set; }
        public DateTime Time { get; private set; }
        public DateTime RawTime { get; private set; } // as Vivox reported it, used for history paging
        internal string PlainText { get; private set; }
        internal ChatMetadata Reply { get; private set; }

        UnityEngine.UI.LayoutElement _rowSize;
        ChatPage _page;
        Sprite _shape;
        MessageActions _actions;
        internal bool PointerInside { get; private set; }
        Button _quote;
        TMP_Text _quoteText;
        RectTransform _reactions;
        readonly List<(Button button, TMP_Text label)> _reactionButtons = new();
        int _highlightVersion;

        void Awake()
        {
            layout.padding = new RectOffset(18, 16, 12, 2);
            layout.spacing = 12f;
            _rowSize = GetComponent<UnityEngine.UI.LayoutElement>();
            if (_rowSize == null) _rowSize = gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            // Stretch the columns inside each row while keeping the row itself at its natural height.
            _rowSize.flexibleHeight = 0f;
            SetHeader(header.activeSelf);

            var timeRect = hoverTime.rectTransform;
            timeRect.offsetMax = new Vector2(8f, timeRect.offsetMax.y);
            UiPresentation.ApplyPalette(gameObject);

            if (GetComponent<UnityEngine.UI.Selectable>() == null)
                gameObject.AddComponent<UnityEngine.UI.Selectable>().transition = UnityEngine.UI.Selectable.Transition.None;
            if (authorButton != null)
                authorButton.onClick.AddListener(() => MainScreen.Instance.ShowProfile(SenderId));
        }

        internal void Attach(ChatPage page, Sprite shape)
        {
            _page = page;
            _shape = shape;
            var font = body.font;
            var textColumn = body.transform.parent;
            _quote = ChatWidgets.Button(textColumn, font, shape, "Original message", -1, 24,
                () => _page.OpenOriginal(Reply), out var quoteText);
            _quoteText = quoteText;
            _quoteText.alignment = TextAlignmentOptions.MidlineLeft;
            _quoteText.color = Theme.TextMuted;
            _quote.transform.SetAsFirstSibling();
            _quote.gameObject.SetActive(false);

            _reactions = ChatWidgets.Rect(textColumn, "Reactions");
            var grid = _reactions.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(64, 26);
            grid.spacing = new Vector2(4, 4);
            grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            _reactions.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerInside = true;
            ShowActions();
        }

        public void OnPointerExit(PointerEventData eventData) => PointerInside = false;

        public void OnSelect(BaseEventData eventData) => ShowActions();

        void OnDisable()
        {
            PointerInside = false;
            if (_actions != null) _actions.gameObject.SetActive(false);
        }

        void ShowActions()
        {
            if (_page == null || !_page.composer.interactable || string.IsNullOrEmpty(MessageId)) return;
            if (_actions == null) _actions = MessageActions.Create(this, _page, _shape);
            _actions.Show();
        }

        public void Set(VivoxMessage message)
        {
            MessageId = message.MessageId;
            SenderId = message.SenderPlayerId;
            RawTime = message.ReceivedTime;
            Time = ToLocal(message.ReceivedTime);

            var name = App.Friends.NameOf(SenderId, message.SenderDisplayName);
            avatar.Set(SenderId, name);
            author.text = name;
            timestamp.text = FormatFull(Time);
            hoverTime.text = Time.ToString("h:mm tt");
            body.richText = false; // message text is shown as typed, never as TMP markup
            Reply = ChatMetadata.ReadReply(message, out var text);
            PlainText = text;
            EmojiCatalog.SetText(body, PlainText);
            if (_quote == null) return;
            _quote.gameObject.SetActive(Reply != null);
            if (Reply != null) UpdateQuote(null);
        }

        internal void UpdateQuote(MessageItem original)
        {
            var name = ChatMetadata.Preview(original != null ? original.author.text : Reply.author, 48);
            var excerpt = ChatMetadata.Preview(original != null ? original.PlainText : Reply.excerpt, 140);
            string caption = name.Length > 0 ? $"↪ {name}  ·  {excerpt}" : "↪ Original message";
            if (original != null && original == this) return;
            EmojiCatalog.SetText(_quoteText, caption);
        }

        internal void SetReactions(IEnumerable<(string emoji, int count, bool own)> reactions)
        {
            int index = 0;
            foreach (var (emoji, count, own) in reactions)
            {
                if (index == _reactionButtons.Count)
                {
                    var button = ChatWidgets.Button(_reactions, body.font, _shape, "Reaction", 64, 26,
                        null, out var label);
                    label.fontSize = 15;
                    _reactionButtons.Add((button, label));
                }
                var chip = _reactionButtons[index++];
                chip.button.gameObject.SetActive(true);
                chip.button.onClick.RemoveAllListeners();
                chip.button.onClick.AddListener(() => _page.ToggleReaction(this, emoji));
                ChatWidgets.Tint(chip.button, own ? Theme.Accent : Theme.UserPanel);
                EmojiCatalog.SetText(chip.label, $"{emoji}  {count}");
            }
            for (int i = index; i < _reactionButtons.Count; i++)
                _reactionButtons[i].button.gameObject.SetActive(false);
            _reactions.gameObject.SetActive(index > 0);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        internal void HighlightOriginal() => HighlightAsync().Forget();

        async UniTaskVoid HighlightAsync()
        {
            var highlight = GetComponent<HoverHighlight>();
            if (highlight == null) return;
            int version = ++_highlightVersion;
            highlight.Selected = true;
            await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true);
            if (highlight != null && version == _highlightVersion) highlight.Selected = false;
        }

        public void SetHeader(bool show)
        {
            header.SetActive(show);
            avatar.gameObject.SetActive(show);
            hoverTime.transform.parent.gameObject.SetActive(!show); // compact rows show the time on hover instead
            layout.padding.top = show ? 12 : 2;
            if (_rowSize != null)
                _rowSize.minHeight = show ? ((RectTransform)avatar.transform).rect.height + layout.padding.vertical : 0f;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        // Same author within 7 minutes continues the previous group.
        public bool ContinuesFrom(MessageItem previous) =>
            Reply == null && previous != null && previous.SenderId == SenderId &&
            (Time - previous.Time).TotalMinutes < 7;

        static DateTime ToLocal(DateTime time) => time.Kind == DateTimeKind.Utc ? time.ToLocalTime() : time;

        static string FormatFull(DateTime time)
        {
            var today = DateTime.Now.Date;
            if (time.Date == today) return "Today at " + time.ToString("h:mm tt");
            if (time.Date == today.AddDays(-1)) return "Yesterday at " + time.ToString("h:mm tt");
            return time.ToString("MM/dd/yyyy h:mm tt");
        }
    }
}
