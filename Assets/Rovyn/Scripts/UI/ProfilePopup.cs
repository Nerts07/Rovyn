using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Profile card for any user. For yourself it also sets status and custom status.
    public class ProfilePopup : MonoBehaviour
    {
        public FadePanel panel;
        public Button backdrop;
        public Image banner;
        public Avatar avatar;
        public TMP_Text displayName;
        public TMP_Text fullName;
        public TMP_Text statusText;
        public TMP_Text playerIdText;
        public Button copyIdButton;

        [Header("Yourself")]
        public GameObject selfSection;
        public Button[] statusButtons;          // Online, Idle, Do Not Disturb, Invisible
        public HoverHighlight[] statusHighlights;
        public TMP_InputField customStatus;
        public Button saveStatusButton;
        public Button editProfileButton;

        [Header("Other users")]
        public GameObject actionsSection;
        public Button messageButton;
        public Button callButton;
        public Button addFriendButton;
        public Button acceptButton;
        public Button declineButton;
        public Button cancelButton;
        public Button removeButton;
        public Button blockButton;
        public Button unblockButton;

        string _id;
        bool IsSelf => _id == App.Auth.PlayerId;

        void Awake()
        {
            var surface = panel.content.GetComponent<UnityEngine.UI.Image>();
            UiPresentation.RoundSurface(surface, surface.sprite, 14f);
            UiPresentation.RoundSurface(banner, banner.sprite, 10f);
            displayName.richText = false;
            fullName.richText = false;
            statusText.richText = false;

            backdrop.onClick.AddListener(Close);
            copyIdButton.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = _id;
                App.Report("Player ID copied.");
            });

            for (int i = 0; i < statusButtons.Length; i++)
            {
                var status = (UserStatus)i; // button order matches the enum
                statusButtons[i].onClick.AddListener(() => SetStatus(status));
            }
            saveStatusButton.onClick.AddListener(() => SetStatus(App.Friends.MyStatus));
            customStatus.onSubmit.AddListener(_ => SetStatus(App.Friends.MyStatus));
            editProfileButton.onClick.AddListener(() => { Close(); MainScreen.Instance.ShowSettings(); });

            messageButton.onClick.AddListener(() => { var id = _id; Close(); MainScreen.Instance.OpenDirect(id); });
            callButton.onClick.AddListener(() => { var id = _id; Close(); MainScreen.Instance.StartCall(id); });
            addFriendButton.onClick.AddListener(() => Act(() => App.Friends.SendRequestAsync(_id), addFriendButton));
            acceptButton.onClick.AddListener(() => Act(() => App.Friends.AcceptAsync(_id), acceptButton));
            declineButton.onClick.AddListener(() => Act(() => App.Friends.DeclineAsync(_id), declineButton));
            cancelButton.onClick.AddListener(() => Act(() => App.Friends.CancelRequestAsync(_id), cancelButton));
            removeButton.onClick.AddListener(() => Act(() => App.Friends.RemoveFriendAsync(_id), removeButton));
            blockButton.onClick.AddListener(() => Act(() => App.Friends.BlockAsync(_id), blockButton));
            unblockButton.onClick.AddListener(() => Act(() => App.Friends.UnblockAsync(_id), unblockButton));
        }

        void OnEnable()
        {
            App.Friends.Changed += Refresh;
            App.Friends.PresenceChanged += OnPresence;
        }

        void OnDisable()
        {
            App.Friends.Changed -= Refresh;
            App.Friends.PresenceChanged -= OnPresence;
        }

        public void Open(string playerId, bool fromChat = false)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            _id = playerId;
            customStatus.text = IsSelf ? App.Friends.MyActivity?.status ?? "" : "";

            var card = panel.content;
            var anchor = new Vector2(fromChat ? 1f : 0.5f, 0.5f);
            card.anchorMin = card.anchorMax = card.pivot = anchor;
            card.anchoredPosition = new Vector2(fromChat ? -24f : 0f, 0f);
            card.sizeDelta = new Vector2(fromChat ? 376f : 440f, card.sizeDelta.y);
            backdrop.targetGraphic.color = fromChat ? new Color(0f, 0f, 0f, 0.35f) : Theme.Backdrop;
            panel.Show();
            Refresh();
        }

        public void Close() => panel.Hide();

        void OnPresence(string id)
        {
            if (id == _id) Refresh();
        }

        void Refresh()
        {
            if (_id == null || !panel.IsOpen) return;

            var name = App.Friends.NameOf(_id);
            var full = App.Friends.FullNameOf(_id);
            var status = App.Friends.StatusOf(_id);
            var activity = App.Friends.ActivityOf(_id);

            banner.color = Theme.AvatarColor(_id);
            avatar.Set(_id, name);
            avatar.SetStatus(status);
            displayName.text = name;
            fullName.text = string.IsNullOrEmpty(full) ? "" : full;
            statusText.text = !string.IsNullOrEmpty(activity?.voice) ? $"In voice: {activity.voice}"
                : !string.IsNullOrEmpty(activity?.status) ? activity.status
                : Theme.StatusLabel(status);
            playerIdText.text = _id;

            selfSection.SetActive(IsSelf);
            actionsSection.SetActive(!IsSelf);
            if (IsSelf)
            {
                for (int i = 0; i < statusHighlights.Length; i++)
                    statusHighlights[i].Selected = (int)App.Friends.MyStatus == i;
                return;
            }

            bool friend = App.Friends.IsFriend(_id);
            bool incoming = App.Friends.HasIncoming(_id);
            bool outgoing = App.Friends.HasOutgoing(_id);
            bool blocked = App.Friends.IsBlocked(_id);

            messageButton.gameObject.SetActive(!blocked);
            callButton.gameObject.SetActive(friend);
            addFriendButton.gameObject.SetActive(!friend && !incoming && !outgoing && !blocked);
            acceptButton.gameObject.SetActive(incoming);
            declineButton.gameObject.SetActive(incoming);
            cancelButton.gameObject.SetActive(outgoing);
            removeButton.gameObject.SetActive(friend);
            blockButton.gameObject.SetActive(!blocked);
            unblockButton.gameObject.SetActive(blocked);
        }

        void SetStatus(UserStatus status)
        {
            UiActions.Run(async () =>
            {
                await App.Friends.SetStatusAsync(status, customStatus.text);
                Refresh();
            });
        }

        void Act(Func<UniTask> action, Button button) => UiActions.Run(async () =>
        {
            await action();
            Refresh();
        }, button);
    }
}
