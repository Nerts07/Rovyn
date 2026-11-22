using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // A profile photo or colored initial, with an optional presence dot and speaking ring.
    public class Avatar : MonoBehaviour
    {
        public Image background;
        public TMP_Text initial;
        public GameObject statusRoot;   // ring in the surrounding background color
        public Image statusDot;
        public Image speakingRing;

        string _player;
        UnityEngine.UI.RawImage _photo;
        internal bool HasPhoto => _photo != null && _photo.texture != null;
        internal event System.Action PhotoChanged;

        void Awake()
        {
            // Layout groups can stretch the avatar slot to a rectangle.
            var square = ChatWidgets.Rect(transform, "Avatar Visuals");
            var fitter = square.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectRatio = 1f;
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;

            if (speakingRing != null) speakingRing.rectTransform.SetParent(square, false);
            background.rectTransform.SetParent(square, false);
            initial.rectTransform.SetParent(square, false);
            if (statusRoot != null) statusRoot.transform.SetParent(square, false);
        }

        public void Set(string playerId, string displayName)
        {
            background.color = Theme.AvatarColor(playerId);
            initial.text = string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1).ToUpperInvariant();
            if (_player == playerId) return;
            if (isActiveAndEnabled) ReleasePhoto();
            _player = playerId;
            if (string.IsNullOrEmpty(_player)) ShowPhoto(null);
            else if (isActiveAndEnabled) BindPhoto();
        }

        void OnEnable() => BindPhoto();
        void OnDisable() => ReleasePhoto();

        void BindPhoto()
        {
            if (!string.IsNullOrEmpty(_player)) ShowPhoto(App.Avatars.Watch(_player, ShowPhoto));
        }

        void ReleasePhoto()
        {
            if (!string.IsNullOrEmpty(_player)) App.Avatars.Unwatch(_player, ShowPhoto);
        }

        void ShowPhoto(Texture2D texture)
        {
            bool hadPhoto = HasPhoto;
            if (texture != null && _photo == null)
            {
                var rect = ChatWidgets.Rect(background.transform.parent, "Profile Photo");
                rect.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);
                ChatWidgets.Stretch(rect, Vector2.zero, Vector2.zero);
                _photo = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                _photo.raycastTarget = false;
            }
            if (_photo != null)
            {
                _photo.texture = texture;
                _photo.enabled = texture != null;
            }
            background.enabled = texture == null;
            initial.gameObject.SetActive(texture == null);
            if (hadPhoto != HasPhoto) PhotoChanged?.Invoke();
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
