using System.Linq;
using UnityEngine;

namespace Rovyn.UI
{
    // Far-left column: home button (friends + DMs) and one button per server.
    public class ServerRail : MonoBehaviour
    {
        public ServerButton homeButton;
        public ServerButton serverPrefab;
        public Transform serverContainer;

        ItemList<ServerButton> _servers;

        void Awake() => homeButton.Clicked = () => MainScreen.Instance.ShowHome();

        void OnEnable()
        {
            App.Chat.UnreadChanged += RefreshBadge;
            App.Friends.Changed += RefreshBadge;
            RefreshBadge();
        }

        void OnDisable()
        {
            App.Chat.UnreadChanged -= RefreshBadge;
            App.Friends.Changed -= RefreshBadge;
        }

        public void Build(ServerCatalog catalog)
        {
            _servers ??= new ItemList<ServerButton>(serverPrefab, serverContainer);
            var indices = Enumerable.Range(0, catalog.servers.Length).ToList();
            _servers.Bind(indices, (button, index) =>
            {
                button.SetServer(catalog.servers[index]);
                button.Clicked = () => MainScreen.Instance.OpenServer(index);
            });
        }

        public void Select(int index)
        {
            homeButton.Selected = index < 0;
            if (_servers == null) return;
            for (int i = 0; i < _servers.Items.Count; i++)
                _servers.Items[i].Selected = i == index;
        }

        // Home shows unread DMs plus incoming friend requests, like Discord.
        void RefreshBadge() => homeButton.SetBadge(App.Chat.TotalDirectUnread + App.Friends.Incoming.Count);
    }
}
