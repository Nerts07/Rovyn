using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Placeholder avatar: a colored circle with the first letter of the name,
    // plus an optional presence dot and speaking ring.
    public class Avatar : MonoBehaviour
    {
        public Image background;
        public TMP_Text initial;
        public GameObject statusRoot;   // ring in the surrounding background color
        public Image statusDot;
        public Image speakingRing;

        public void Set(string playerId, string displayName)
        {
            background.color = Theme.AvatarColor(playerId);
            initial.text = string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1).ToUpperInvariant();
        }

        public void SetStatus(UserStatus? status)
        {
            if (statusRoot == null) return;
            statusRoot.SetActive(status.HasValue);
            if (status.HasValue) statusDot.color = Theme.StatusColor(status.Value);
        }

        public void SetSpeaking(bool speaking)
        {
            if (speakingRing != null) speakingRing.enabled = speaking;
        }
    }
}
