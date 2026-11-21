using TMPro;
using Unity.Services.Friends.Models;
using UnityEngine.UI;
using UnityEngine;

namespace Rovyn.UI
{
    public class ActiveNowCard : MonoBehaviour
    {
        public Button cardButton;
        public Avatar avatar;
        public TMP_Text nameText;
        public TMP_Text activityText;
        public Button joinButton;

        string _id;
        string _channel;
        string _label;

        void Awake()
        {
            cardButton.onClick.AddListener(() => MainScreen.Instance.ShowProfile(_id));
            joinButton.onClick.AddListener(Join);
        }

        public void Bind(Member member, PresenceActivity activity)
        {
            _id = member.Id;
            // Only offer to join server channels, never whatever channel name a presence payload contains.
            _channel = activity.voiceChannel != null && activity.voiceChannel.StartsWith("srv.") ? activity.voiceChannel : null;
            _label = activity.voice;
            var name = App.Friends.NameOf(member.Id);
            avatar.Set(member.Id, name);
            avatar.SetStatus(FriendsStore.StatusOf(member));
            nameText.text = name;
            activityText.text = string.IsNullOrEmpty(_channel) ? "In a private call" : $"In voice: {activity.voice}";
            joinButton.gameObject.SetActive(!string.IsNullOrEmpty(_channel) && App.Voice.Channel != _channel);
        }

        void Join()
        {
            if (string.IsNullOrEmpty(_channel)) return;
            MainScreen.Instance.ShowVoice();
            UiActions.Run(async () =>
            {
                if (App.Calls.State != CallState.None) await App.Calls.HangUpAsync();
                await App.Voice.JoinAsync(_channel, _label);
            }, joinButton);
        }
    }
}
