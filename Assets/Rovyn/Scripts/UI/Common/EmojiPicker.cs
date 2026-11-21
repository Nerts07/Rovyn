using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rovyn.UI
{
    internal sealed class EmojiPicker : MonoBehaviour
    {
        const int MaxColumns = 8;
        const int RowHeight = 36;
        const int CategoryWidth = 38;
        const int CategoryHeight = 29;
        const int PanelPadding = 12;
        const int PanelSpacing = 6;
        const int HeaderHeight = 28;
        const int SearchHeight = 34;
        const int PanelChrome = 2 * (PanelPadding + PanelSpacing) + HeaderHeight + SearchHeight;
        const int MaxPanelHeight = 376;
        const int PooledRows = (MaxPanelHeight - PanelChrome + RowHeight - 1) / RowHeight + 2;

        static readonly (string name, string icon)[] Categories =
        {
            ("Smileys & Emotion", "😀"),
            ("People & Body", "👋"),
            ("Animals & Nature", "🌿"),
            ("Food & Drink", "🍕"),
            ("Travel & Places", "🚲"),
            ("Activities", "🎮"),
            ("Objects", "💡"),
            ("Symbols", "💜"),
            ("Flags", "🏁"),
        };

        readonly struct ResultRow
        {
            internal readonly int Start;
            internal readonly int Count;
            internal readonly string Category;

            internal ResultRow(int start, int count, string category)
            {
                Start = start;
                Count = count;
                Category = category;
            }
        }

        sealed class RowView
        {
            internal RectTransform Root;
            internal TMP_Text Heading;
            internal readonly UnityEngine.UI.Button[] Buttons = new UnityEngine.UI.Button[MaxColumns];
            internal readonly TMP_Text[] Captions = new TMP_Text[MaxColumns];
            internal int Index = -1;
        }

        readonly List<EmojiCatalog.Entry> _matches = new();
        readonly List<ResultRow> _rows = new();
        readonly RowView[] _rowViews = new RowView[PooledRows];
        readonly Dictionary<string, int> _categoryRows = new(StringComparer.Ordinal);
        readonly Dictionary<string, UnityEngine.UI.Button> _categoryButtons = new(StringComparer.Ordinal);
        RectTransform _panel;
        RectTransform _content;
        UnityEngine.UI.ScrollRect _scroll;
        UnityEngine.UI.LayoutElement _resultsSize;
        TMP_InputField _search;
        TMP_Text _empty;
        Action<string> _chosen;
        string _activeCategory;
        int _columns = MaxColumns;

        internal static EmojiPicker Create(RectTransform parent, TMP_FontAsset font, Sprite shape)
        {
            var root = ChatWidgets.Rect(parent, "Emoji Picker");
            ChatWidgets.Size(root, -1, -1).ignoreLayout = true;
            ChatWidgets.Stretch(root, Vector2.zero, Vector2.zero);
            var picker = root.gameObject.AddComponent<EmojiPicker>();
            picker.Build(root, font, shape);
            root.gameObject.SetActive(false);
            return picker;
        }

        void Build(RectTransform root, TMP_FontAsset font, Sprite shape)
        {
            var backdrop = root.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(0, 0, 0, 0.24f);
            var dismiss = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            dismiss.transition = UnityEngine.UI.Selectable.Transition.None;
            dismiss.onClick.AddListener(Close);

            _panel = ChatWidgets.Rect(root, "Panel");
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(1, 0);
            _panel.anchoredPosition = new Vector2(-16, 76);
            var bg = _panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            bg.color = Theme.Popup;
            UiPresentation.RoundSurface(bg, shape, 10f);
            // Consume clicks on the panel's padding without triggering the backdrop.
            _panel.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var stack = _panel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.padding = new RectOffset(PanelPadding, PanelPadding, PanelPadding, PanelPadding);
            stack.spacing = PanelSpacing;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var titleRow = Row(_panel, "Header");
            var title = ChatWidgets.Label(ChatWidgets.Rect(titleRow, "Title"), font, 16, Theme.TextHeader);
            title.text = "Emoji";
            ChatWidgets.Size(title.rectTransform, -1, HeaderHeight).flexibleWidth = 1;
            ChatWidgets.Button(titleRow, font, shape, "Close", 60, HeaderHeight, Close, out _);
            _search = Search(_panel, font, shape);

            var browser = ChatWidgets.Rect(_panel, "Emoji Browser");
            _resultsSize = ChatWidgets.Size(browser, -1, 0);
            BuildCategories(browser, font, shape);

            var results = ChatWidgets.Rect(browser, "Emoji Results");
            ChatWidgets.Stretch(results, new Vector2(CategoryWidth + PanelSpacing, 0), Vector2.zero);
            _scroll = CreateScroll(results, out _content);
            UiPresentation.AddScrollbars(results.gameObject, shape);
            _scroll.onValueChanged.AddListener(_ => RefreshVisibleRows());

            var emptyRect = ChatWidgets.Rect(_scroll.viewport, "No Results");
            ChatWidgets.Stretch(emptyRect, Vector2.zero, Vector2.zero);
            _empty = ChatWidgets.Label(emptyRect, font, 14, Theme.TextMuted, TextAlignmentOptions.Center);
            _empty.text = "No matching emoji";

            for (int i = 0; i < _rowViews.Length; i++)
                _rowViews[i] = BuildResultRow(font, shape);
        }

        void BuildCategories(RectTransform parent, TMP_FontAsset font, Sprite shape)
        {
            var rail = ChatWidgets.Rect(parent, "Emoji Categories");
            rail.anchorMin = Vector2.zero;
            rail.anchorMax = Vector2.up;
            rail.offsetMin = Vector2.zero;
            rail.offsetMax = new Vector2(CategoryWidth, 0);
            var background = rail.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = Theme.Sidebar;
            UiPresentation.RoundSurface(background, shape, 8f);

            var scroll = CreateScroll(rail, out var content);
            scroll.scrollSensitivity = CategoryHeight;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.sizeDelta = new Vector2(0, Categories.Length * CategoryHeight + layout.padding.vertical);
            foreach (var (name, icon) in Categories)
            {
                var button = ChatWidgets.Button(content, font, shape, name, -1, CategoryHeight,
                    () => JumpToCategory(name), out var label);
                label.fontSize = 19;
                EmojiCatalog.SetText(label, icon);
                _categoryButtons.Add(name, button);
            }
        }

        static UnityEngine.UI.ScrollRect CreateScroll(RectTransform root, out RectTransform content)
        {
            var viewport = ChatWidgets.Rect(root, "Viewport");
            ChatWidgets.Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Theme.Clear;

            content = ChatWidgets.Rect(viewport, "Content");
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            return scroll;
        }

        RowView BuildResultRow(TMP_FontAsset font, Sprite shape)
        {
            var view = new RowView { Root = ChatWidgets.Rect(_content, "Emoji Row") };
            view.Root.anchorMin = Vector2.up;
            view.Root.anchorMax = Vector2.one;
            var heading = ChatWidgets.Rect(view.Root, "Category");
            ChatWidgets.Stretch(heading, new Vector2(6, 0), Vector2.zero);
            view.Heading = ChatWidgets.Label(heading, font, 13, Theme.TextHeader);
            view.Heading.fontStyle = FontStyles.Bold;

            for (int i = 0; i < MaxColumns; i++)
            {
                int column = i;
                view.Buttons[i] = ChatWidgets.Button(view.Root, font, shape, "Emoji", -1, -1,
                    () => Pick(view, column), out var caption);
                ChatWidgets.Stretch(caption.rectTransform, Vector2.zero, Vector2.zero);
                caption.fontSize = 23;
                var colors = view.Buttons[i].colors;
                colors.normalColor = Theme.Clear;
                view.Buttons[i].colors = colors;
                view.Captions[i] = caption;
            }
            view.Root.gameObject.SetActive(false);
            return view;
        }

        TMP_InputField Search(Transform parent, TMP_FontAsset font, Sprite shape)
        {
            var rect = ChatWidgets.Rect(parent, "Search Emoji");
            ChatWidgets.Size(rect, -1, SearchHeight);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Theme.Input;
            UiPresentation.RoundSurface(image, shape, 6f);
            var viewport = ChatWidgets.Rect(rect, "Text Area");
            ChatWidgets.Stretch(viewport, new Vector2(10, 0), new Vector2(-10, 0));
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var valueRect = ChatWidgets.Rect(viewport, "Text");
            var placeholderRect = ChatWidgets.Rect(viewport, "Placeholder");
            ChatWidgets.Stretch(valueRect, Vector2.zero, Vector2.zero);
            ChatWidgets.Stretch(placeholderRect, Vector2.zero, Vector2.zero);
            var value = ChatWidgets.Label(valueRect, font, 14, Theme.TextNormal);
            value.overflowMode = TextOverflowModes.Overflow;
            var placeholder = ChatWidgets.Label(placeholderRect, font, 14, Theme.TextMuted);
            placeholder.text = "Search emoji by name or category";
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.transition = UnityEngine.UI.Selectable.Transition.None;
            input.textViewport = viewport;
            input.textComponent = value;
            input.placeholder = placeholder;
            input.onValueChanged.AddListener(Filter);
            UiPresentation.AddSearchClear(input);
            return input;
        }

        static RectTransform Row(Transform parent, string name)
        {
            var rect = ChatWidgets.Rect(parent, name);
            var row = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            row.spacing = 6;
            return rect;
        }

        internal void Show(Action<string> chosen)
        {
            Resize();
            _search.SetTextWithoutNotify("");
            // Notify the field's clear control too when reopening with a previous search.
            _search.onValueChanged.Invoke("");
            _chosen = chosen;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Resize();
            _search.Select();
            _search.ActivateInputField();
        }

        internal void Close()
        {
            _chosen = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        void OnRectTransformDimensionsChange()
        {
            if (_rowViews[PooledRows - 1] != null && isActiveAndEnabled) Resize();
        }

        void Resize()
        {
            var bounds = ((RectTransform)transform).rect;
            float width = Mathf.Clamp(bounds.width - 32, 160, 388);
            float height = Mathf.Clamp(bounds.height - 92, 203, MaxPanelHeight);
            // The shared scrollbar reserves a 12-pixel gutter in the results viewport.
            float resultsWidth = width - 2 * PanelPadding - CategoryWidth - PanelSpacing - 12;
            int columns = Mathf.Clamp(Mathf.FloorToInt(resultsWidth / RowHeight), 1, MaxColumns);
            int previousRow = FirstVisibleRow();

            _panel.sizeDelta = new Vector2(width, height);
            _resultsSize.preferredHeight = height - PanelChrome;
            if (_columns == columns)
            {
                RefreshVisibleRows();
                return;
            }

            var anchor = _rows.Count > 0 ? _rows[previousRow] : default;
            _columns = columns;
            BuildRows();
            int row = _rows.FindIndex(item => item.Count == 0
                ? anchor.Count == 0 && item.Category == anchor.Category
                : anchor.Count > 0 && item.Start <= anchor.Start && anchor.Start < item.Start + item.Count);
            GoToRow(Mathf.Max(0, row));
        }

        void Filter(string query)
        {
            _matches.Clear();
            query = query.Trim();
            var emoji = EmojiCatalog.Canonical(query);
            foreach (var entry in EmojiCatalog.Entries)
            {
                if (query.Length == 0 || entry.Text == emoji ||
                    entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Group.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    _matches.Add(entry);
            }
            BuildRows();
            GoToRow(0);
        }

        void BuildRows()
        {
            _rows.Clear();
            _categoryRows.Clear();
            for (int start = 0; start < _matches.Count;)
            {
                string name = _matches[start].Group;
                _categoryRows.Add(name, _rows.Count);
                _rows.Add(new ResultRow(start, 0, name));

                int end = start + 1;
                while (end < _matches.Count && _matches[end].Group == name) end++;
                for (int index = start; index < end; index += _columns)
                    _rows.Add(new ResultRow(index, Mathf.Min(_columns, end - index), name));
                start = end;
            }

            foreach (var category in _categoryButtons)
                category.Value.interactable = _categoryRows.ContainsKey(category.Key);
            foreach (var view in _rowViews)
                view.Index = -1;
            _content.sizeDelta = new Vector2(0, _rows.Count * RowHeight);
            _empty.gameObject.SetActive(_rows.Count == 0);
        }

        void JumpToCategory(string name)
        {
            if (_categoryRows.TryGetValue(name, out int row)) GoToRow(row);
        }

        void GoToRow(int row)
        {
            _scroll.StopMovement();
            float maxOffset = Mathf.Max(0, _content.sizeDelta.y - _resultsSize.preferredHeight);
            _content.anchoredPosition = new Vector2(0, Mathf.Clamp(row * RowHeight, 0, maxOffset));
            RefreshVisibleRows();
        }

        int FirstVisibleRow()
        {
            return Mathf.Clamp(Mathf.FloorToInt(_content.anchoredPosition.y / RowHeight), 0,
                Mathf.Max(0, _rows.Count - 1));
        }

        void RefreshVisibleRows()
        {
            int firstVisible = FirstVisibleRow();
            int start = Mathf.Max(0, firstVisible - 1);
            for (int i = start; i < start + _rowViews.Length; i++)
            {
                var view = _rowViews[i % _rowViews.Length];
                if (i >= _rows.Count)
                {
                    view.Index = -1;
                    view.Root.gameObject.SetActive(false);
                }
                else if (view.Index != i)
                {
                    BindRow(view, i);
                }
            }
            SelectCategory(_rows.Count > 0 ? _rows[firstVisible].Category : null);
        }

        void BindRow(RowView view, int index)
        {
            var row = _rows[index];
            view.Index = index;
            view.Root.offsetMin = new Vector2(0, -(index + 1) * RowHeight);
            view.Root.offsetMax = new Vector2(0, -index * RowHeight);
            view.Heading.gameObject.SetActive(row.Count == 0);
            if (row.Count == 0) view.Heading.text = row.Category;

            for (int column = 0; column < MaxColumns; column++)
            {
                var button = view.Buttons[column];
                bool visible = column < row.Count;
                button.gameObject.SetActive(visible);
                if (!visible) continue;

                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2((float)column / _columns, 0);
                rect.anchorMax = new Vector2((column + 1f) / _columns, 1);
                rect.offsetMin = new Vector2(2, 2);
                rect.offsetMax = new Vector2(-2, -2);
                var emoji = _matches[row.Start + column];
                button.name = emoji.Name;
                EmojiCatalog.SetText(view.Captions[column], emoji.Text);
            }
            view.Root.gameObject.SetActive(true);
        }

        void SelectCategory(string name)
        {
            if (_activeCategory == name) return;
            if (_activeCategory != null && _categoryButtons.TryGetValue(_activeCategory, out var previous))
                ChatWidgets.Tint(previous, Theme.UserPanel);
            _activeCategory = name;
            if (name != null && _categoryButtons.TryGetValue(name, out var button))
                ChatWidgets.Tint(button, Theme.Accent);
        }

        void Pick(RowView view, int column)
        {
            if (_chosen == null || view.Index < 0 || view.Index >= _rows.Count) return;
            var row = _rows[view.Index];
            if (column >= row.Count) return;

            var chosen = _chosen;
            var emoji = _matches[row.Start + column].Text;
            Close();
            chosen?.Invoke(emoji);
        }
    }
}
