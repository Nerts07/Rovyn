using Unity.Services.Vivox;
using UnityEngine;

namespace Rovyn.UI
{
    [DisallowMultipleComponent]
    internal sealed class NotificationSound : MonoBehaviour
    {
        const string ResourcePath = "discord-notification";

        AudioSource _source;

        void Awake()
        {
            var clip = Resources.Load<AudioClip>(ResourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"Couldn't load notification sound from Resources/{ResourcePath}.");
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.7f;
            _source.clip = clip;
        }

        void OnEnable()
        {
            App.Chat.MessageReceived += OnMessageReceived;
            App.Friends.FriendRequestReceived += PlayIfUnfocused;
        }

        void OnDisable()
        {
            App.Chat.MessageReceived -= OnMessageReceived;
            App.Friends.FriendRequestReceived -= PlayIfUnfocused;
            if (_source != null) _source.Stop();
        }

        void OnMessageReceived(string key, VivoxMessage message)
        {
            if (message.FromSelf || ChatMetadata.IsReaction(message) || App.Friends.IsBlocked(message.SenderPlayerId)) return;
            PlayIfUnfocused();
        }

        void PlayIfUnfocused()
        {
            if (!Application.isFocused && App.SessionActive && _source != null)
                _source.PlayOneShot(_source.clip);
        }
    }
}
