using System.Linq;
using TMPro;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Voice channel / call view: participant grid plus mute, deafen and disconnect controls.
    public class VoicePage : MonoBehaviour
    {
        public TMP_Text title;
        public TMP_Text subtitle;
        public RectTransform gridArea;
        public GridLayoutGroup grid;
        public ParticipantTile tilePrefab;
        public ParticipantTile ringingTile;
        public GameObject emptyState;
        public GameObject controls;

        public Button muteButton;
        public Image muteIcon;
        public Sprite micOn;
        public Sprite micOff;
        public Button deafenButton;
        public Image deafenIcon;
        public Sprite headphonesOn;
        public Sprite headphonesOff;
        public Button settingsButton;
        public Button leaveButton;

        ItemList<ParticipantTile> _tiles;
        Transform _standaloneParent;
        RectTransform _rect;
        UnityEngine.UI.LayoutElement _callLayout;
        UnityEngine.UI.Image _callBackground;

        internal bool IsEmbedded { get; private set; }

        void Awake()
        {
            if (!IsEmbedded) title.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
            _tiles = new ItemList<ParticipantTile>(tilePrefab, grid.transform);
            muteButton.onClick.AddListener(() => App.Voice.SetMuted(!(App.Voice.IsMuted || App.Voice.IsDeafened)));
            deafenButton.onClick.AddListener(() => App.Voice.SetDeafened(!App.Voice.IsDeafened));
            settingsButton.onClick.AddListener(() => MainScreen.Instance.settings.Open(SettingsScreen.Page.Voice));
            leaveButton.onClick.AddListener(() => UiActions.Run(async () =>
            {
                if (App.Calls.State != CallState.None) await App.Calls.HangUpAsync();
                else await App.Voice.LeaveAsync();
            }, leaveButton));
        }

        void OnEnable()
        {
            App.Voice.StateChanged += Refresh;
            App.Voice.ParticipantsChanged += Refresh;
            App.Calls.StateChanged += Refresh;
            App.Vivox.StateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            App.Voice.StateChanged -= Refresh;
            App.Voice.ParticipantsChanged -= Refresh;
            App.Calls.StateChanged -= Refresh;
            App.Vivox.StateChanged -= Refresh;
        }

        void OnRectTransformDimensionsChange()
        {
            if (_tiles != null && isActiveAndEnabled) UpdateGrid();
        }

        internal void EmbedIn(ChatPage chat)
        {
            bool embedded = chat != null;
            if (IsEmbedded == embedded) return;

            if (_rect == null)
            {
                _rect = (RectTransform)transform;
                _standaloneParent = transform.parent;
                _callLayout = gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                _callLayout.flexibleHeight = 0;
                _callBackground = gameObject.AddComponent<UnityEngine.UI.Image>();
                _callBackground.color = Theme.Rail;
                _callBackground.raycastTarget = false;
            }

            IsEmbedded = embedded;
            _callBackground.enabled = embedded;
            title.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = embedded ? 34f : 56f;
            controls.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = embedded ? 54f : 88f;
            controls.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing = embedded ? 8f : 16f;
            gridArea.offsetMin = new Vector2(16f, embedded ? 2f : 16f);
            gridArea.offsetMax = -gridArea.offsetMin;

            if (embedded)
            {
                var messages = chat.scroll.transform.parent;
                _rect.SetParent(chat.transform, false);
                _rect.SetSiblingIndex(messages.GetSiblingIndex());
                ResizeCallPanel();
            }
            else
            {
                _rect.SetParent(_standaloneParent, false);
                _rect.anchorMin = Vector2.zero;
                _rect.anchorMax = Vector2.one;
                _rect.offsetMin = _rect.offsetMax = Vector2.zero;
            }

            Refresh();
        }

        internal void ResizeCallPanel()
        {
            if (!IsEmbedded || transform.parent == _standaloneParent) return;
            float available = ((RectTransform)transform.parent).rect.height;
            _callLayout.preferredHeight = Mathf.Clamp(available * 0.36f, 122f, 248f);
        }

        void Refresh()
        {
            if (_tiles == null) return;
            bool inVoice = App.Voice.Channel != null && (!IsEmbedded || App.Voice.IsPrivate);
            bool showCall = inVoice || IsEmbedded;
            emptyState.SetActive(!showCall);
            controls.SetActive(showCall);

            title.text = IsEmbedded ? "Voice call"
                : !inVoice ? "Voice"
                : App.Voice.IsPrivate ? $"Call with {App.Voice.Label}"
                : App.Voice.Label;
            subtitle.text = !showCall ? ""
                : App.Voice.IsReconnecting || App.Vivox.State == ConnectionState.Recovering ? "Reconnecting…"
                : App.Voice.IsConnecting || !inVoice ? "Connecting…"
                : App.Calls.State == CallState.Outgoing ? "Calling…"
                : $"{App.Voice.Participants.Count} connected";

            var participants = inVoice ? App.Voice.Participants.OrderByDescending(p => p.IsSelf).ToList() : new System.Collections.Generic.List<VivoxParticipant>();
            _tiles.Bind(participants, (tile, p) => tile.Bind(p));

            bool ringing = App.Calls.State == CallState.Outgoing && participants.All(p => p.PlayerId != App.Calls.PeerId);
            ringingTile.gameObject.SetActive(ringing);
            if (ringing)
            {
                ringingTile.BindRinging(App.Calls.PeerId);
                ringingTile.transform.SetAsLastSibling();
            }

            bool micMuted = App.Voice.IsMuted || App.Voice.IsDeafened;
            muteIcon.sprite = micMuted ? micOff : micOn;
            deafenIcon.sprite = App.Voice.IsDeafened ? headphonesOff : headphonesOn;
            UpdateGrid();
        }

        // Fit tiles into the available area at 16:9, Discord-style.
        void UpdateGrid()
        {
            int count = grid.transform.Cast<Transform>().Count(t => t.gameObject.activeSelf);
            if (count == 0) return;
            var area = gridArea.rect;
            float spacing = grid.spacing.x;
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int rows = Mathf.CeilToInt(count / (float)columns);
            float width = (area.width - spacing * (columns - 1)) / columns;
            float height = (area.height - spacing * (rows - 1)) / rows;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            if (IsEmbedded)
                grid.cellSize = new Vector2(Mathf.Clamp(width, 1f, 280f), Mathf.Clamp(height, 1f, 140f));
            else
            {
                width = Mathf.Clamp(Mathf.Min(width, height * 16f / 9f), 120f, 640f);
                grid.cellSize = new Vector2(width, width * 9f / 16f);
            }

            foreach (Transform child in grid.transform)
                if (child.gameObject.activeSelf && child.TryGetComponent<ParticipantTile>(out var tile))
                    tile.SetCompact(IsEmbedded, grid.cellSize.y);
        }
    }
}
