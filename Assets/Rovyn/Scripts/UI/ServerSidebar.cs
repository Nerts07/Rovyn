using System;
using TMPro;
using Unity.Services.Vivox;
using UnityEngine;

namespace Rovyn.UI
{
    // Secondary sidebar in a server: text and voice channels.
    public class ServerSidebar : MonoBehaviour
    {
        public TMP_Text serverName;
        public ChannelEntry channelPrefab;
        public Transform textContainer;
        public Transform voiceContainer;
        public Sprite textIcon;
        public Sprite voiceIcon;

        ItemList<ChannelEntry> _text;
        ItemList<ChannelEntry> _voice;
        ServerCatalog.Server _server;
        string _selected;

        void Awake()
        {
            serverName.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
            _text = new ItemList<ChannelEntry>(channelPrefab, textContainer);
            _voice = new ItemList<ChannelEntry>(channelPrefab, voiceContainer);
        }

        void OnEnable()
        {
            App.Voice.StateChanged += Refresh;
            App.Voice.ParticipantsChanged += Refresh;
            App.Chat.UnreadChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            App.Voice.StateChanged -= Refresh;
            App.Voice.ParticipantsChanged -= Refresh;
            App.Chat.UnreadChanged -= Refresh;
        }

        public void Show(ServerCatalog.Server server)
        {
            _server = server;
            serverName.text = server.name;
            Refresh();
        }

        public void SetSelection(string textChannel)
        {
            _selected = textChannel;
            Refresh();
        }

        void Refresh()
        {
            if (_server == null || _text == null || !isActiveAndEnabled) return;
            var server = _server;

            _text.Bind(server.textChannels, (entry, channel) =>
            {
                var key = ChatService.ChannelKey(ServerCatalog.TextChannelId(server, channel));
                entry.Bind(channel, textIcon, channel == _selected, App.Chat.UnreadCount(key) > 0,
                    () => MainScreen.Instance.OpenTextChannel(channel));
                entry.BindUsers(Array.Empty<VivoxParticipant>());
            });

            _voice.Bind(server.voiceChannels, (entry, channel) =>
            {
                bool connected = App.Voice.Channel == ServerCatalog.VoiceChannelId(server, channel);
                entry.Bind(channel, voiceIcon, false, false, () => MainScreen.Instance.JoinVoiceChannel(channel));
                // Vivox only reports participants for channels we're in, so only the joined channel lists users.
                entry.BindUsers(connected ? App.Voice.Participants : Array.Empty<VivoxParticipant>());
            });
        }
    }
}
