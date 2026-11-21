using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // One row in the direct messages list.
    public class DmEntry : MonoBehaviour
    {
        public Button button;
        public HoverHighlight highlight;
        public Avatar avatar;
        public TMP_Text nameText;
        public TMP_Text subtitle;
        public GameObject unreadBadge;
        public TMP_Text unreadText;
        public Button closeButton;

        string _playerId;

        void Awake()
        {
            button.onClick.AddListener(() => MainScreen.Instance.OpenDirect(_playerId));
            closeButton.onClick.AddListener(() => App.Chat.RemovePartner(_playerId));
        }

        public void Bind(string playerId, bool selected)
        {
            _playerId = playerId;
            var name = App.Friends.NameOf(playerId);
            avatar.Set(playerId, name);
            avatar.SetStatus(App.Friends.StatusOf(playerId));
            nameText.text = name;

            var activity = App.Friends.ActivityOf(playerId);
            var line = activity?.voice != null ? "In voice" : activity?.status;
            subtitle.gameObject.SetActive(!string.IsNullOrEmpty(line));
            subtitle.text = line;

            int unread = App.Chat.UnreadCount(ChatService.DirectKey(playerId));
            unreadBadge.SetActive(unread > 0);
            unreadText.text = unread > 99 ? "99+" : unread.ToString();
            nameText.fontStyle = unread > 0 ? FontStyles.Bold : FontStyles.Normal;

            highlight.Selected = selected;
        }
    }
}
