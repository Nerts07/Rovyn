using System;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Text or voice channel row in the server sidebar. Voice rows list the connected users below.
    public class ChannelEntry : MonoBehaviour
    {
        public Button button;
        public HoverHighlight highlight;
        public Image icon;
        public TMP_Text label;
        public GameObject unreadPill;
        public Transform usersContainer;
        public VoiceUserEntry userPrefab;

        ItemList<VoiceUserEntry> _users;
        Action _onClick;

        void Awake() => button.onClick.AddListener(() => _onClick?.Invoke());

        public void Bind(string name, Sprite iconSprite, bool selected, bool unread, Action onClick)
        {
            label.text = name;
            icon.sprite = iconSprite;
            highlight.Selected = selected;
            unreadPill.SetActive(unread && !selected);
            label.fontStyle = unread ? FontStyles.Bold : FontStyles.Normal;
            _onClick = onClick;
        }

        public void BindUsers(IReadOnlyList<VivoxParticipant> users)
        {
            _users ??= new ItemList<VoiceUserEntry>(userPrefab, usersContainer);
            _users.Bind(users, (entry, p) => entry.Bind(p));
            usersContainer.gameObject.SetActive(users.Count > 0);
        }
    }
}
