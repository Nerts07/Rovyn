using System.Linq;
using UnityEngine;

namespace Rovyn.UI
{
    // Right-hand column on the friends page: friends who are in voice right now.
    public class ActiveNowPanel : MonoBehaviour
    {
        public ActiveNowCard cardPrefab;
        public Transform container;
        public GameObject emptyState;

        ItemList<ActiveNowCard> _cards;

        void Awake() => _cards = new ItemList<ActiveNowCard>(cardPrefab, container);

        void OnEnable()
        {
            App.Friends.Changed += Refresh;
            App.Friends.PresenceChanged += OnPresence;
            Refresh();
        }

        void OnDisable()
        {
            App.Friends.Changed -= Refresh;
            App.Friends.PresenceChanged -= OnPresence;
        }

        void OnPresence(string _) => Refresh();

        void Refresh()
        {
            var active = App.Friends.Friends
                .Select(r => (member: r.Member, activity: FriendsStore.ActivityOf(r.Member)))
                .Where(x => !string.IsNullOrEmpty(x.activity?.voice))
                .ToList();
            _cards.Bind(active, (card, x) => card.Bind(x.member, x.activity));
            emptyState.SetActive(active.Count == 0);
        }
    }
}
