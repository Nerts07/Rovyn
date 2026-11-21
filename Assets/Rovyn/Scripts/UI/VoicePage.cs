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

        void Awake()
        {
            title.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
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
            Refresh();
        }

        void OnDisable()
        {
            App.Voice.StateChanged -= Refresh;
            App.Voice.ParticipantsChanged -= Refresh;
            App.Calls.StateChanged -= Refresh;
        }

        void OnRectTransformDimensionsChange()
        {
            if (_tiles != null && isActiveAndEnabled) UpdateGrid();
        }

        void Refresh()
        {
            if (_tiles == null) return;
            bool inVoice = App.Voice.Channel != null;
            emptyState.SetActive(!inVoice);
            controls.SetActive(inVoice);

            title.text = !inVoice ? "Voice"
                : App.Voice.IsPrivate ? $"Call with {App.Voice.Label}"
                : App.Voice.Label;
            subtitle.text = !inVoice ? ""
                : App.Voice.IsConnecting ? "Connecting…"
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
            width = Mathf.Min(width, height * 16f / 9f, 640f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(Mathf.Max(width, 120f), Mathf.Max(width, 120f) * 9f / 16f);
        }
    }
}
