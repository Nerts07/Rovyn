using TMPro;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    public enum FriendRowKind { Friend, Incoming, Outgoing, Blocked }

    // One person in the friends list, with the actions that fit the relationship.
    public class FriendRow : MonoBehaviour
    {
        public Button rowButton;
        public Avatar avatar;
        public TMP_Text nameText;
        public TMP_Text tagText;      // "#1234", visible on hover
        public TMP_Text subtitle;

        public Button messageButton;
        public Button callButton;
        public Button moreButton;
        public Button acceptButton;
        public Button declineButton;
        public Button cancelButton;
        public Button unblockButton;

        string _id;
        FriendRowKind _kind;

        void Awake()
        {
            rowButton.onClick.AddListener(OnRowClicked);
            messageButton.onClick.AddListener(() => MainScreen.Instance.OpenDirect(_id));
            callButton.onClick.AddListener(() => MainScreen.Instance.StartCall(_id));
            moreButton.onClick.AddListener(() => MainScreen.Instance.ShowProfile(_id));
            acceptButton.onClick.AddListener(() => UiActions.Run(() => App.Friends.AcceptAsync(_id), acceptButton));
            declineButton.onClick.AddListener(() => UiActions.Run(() => App.Friends.DeclineAsync(_id), declineButton));
            cancelButton.onClick.AddListener(() => UiActions.Run(() => App.Friends.CancelRequestAsync(_id), cancelButton));
            unblockButton.onClick.AddListener(() => UiActions.Run(() => App.Friends.UnblockAsync(_id), unblockButton));
        }

        public void Bind(Relationship relationship, FriendRowKind kind)
        {
            var member = relationship.Member;
            _id = member.Id;
            _kind = kind;

            var fullName = member.Profile?.Name;
            var name = string.IsNullOrEmpty(fullName) ? Names.FromId(_id) : Names.Display(fullName);
            var tag = Names.Tag(fullName);
            avatar.Set(_id, name);
            nameText.text = name;
            tagText.text = string.IsNullOrEmpty(tag) ? "" : "#" + tag;

            var status = FriendsStore.StatusOf(member);
            avatar.SetStatus(kind == FriendRowKind.Friend ? status : (UserStatus?)null);
            subtitle.text = kind switch
            {
                FriendRowKind.Incoming => "Incoming Friend Request",
                FriendRowKind.Outgoing => "Outgoing Friend Request",
                FriendRowKind.Blocked => "Blocked",
                _ => FriendSubtitle(member, status),
            };

            messageButton.gameObject.SetActive(kind == FriendRowKind.Friend);
            callButton.gameObject.SetActive(kind == FriendRowKind.Friend);
            moreButton.gameObject.SetActive(kind == FriendRowKind.Friend);
            acceptButton.gameObject.SetActive(kind == FriendRowKind.Incoming);
            declineButton.gameObject.SetActive(kind == FriendRowKind.Incoming);
            cancelButton.gameObject.SetActive(kind == FriendRowKind.Outgoing);
            unblockButton.gameObject.SetActive(kind == FriendRowKind.Blocked);
        }

        static string FriendSubtitle(Member member, UserStatus status)
        {
            var activity = FriendsStore.ActivityOf(member);
            if (!string.IsNullOrEmpty(activity?.voice)) return $"In voice: {activity.voice}";
            if (!string.IsNullOrEmpty(activity?.status)) return activity.status;
            return Theme.StatusLabel(status);
        }

        void OnRowClicked()
        {
            if (_kind == FriendRowKind.Friend) MainScreen.Instance.OpenDirect(_id);
            else MainScreen.Instance.ShowProfile(_id);
        }
    }
}
