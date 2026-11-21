using UnityEngine;

namespace Rovyn.UI
{
    [DisallowMultipleComponent]
    internal sealed class CallRingtone : MonoBehaviour
    {
        const string ResourcePath = "discordcall";

        ResourceRequest _request;
        AudioSource _source;

        void OnEnable()
        {
            App.Calls.StateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            App.Calls.StateChanged -= Refresh;
            if (_source != null) _source.Stop();
        }

        void OnDestroy()
        {
            if (_request != null) _request.completed -= OnClipLoaded;
        }

        void Refresh()
        {
            bool ringing = App.Calls.State is CallState.Incoming or CallState.Outgoing;
            if (!ringing)
            {
                if (_source != null) _source.Stop();
                return;
            }

            if (_source == null)
            {
                if (_request != null) return;
                _request = Resources.LoadAsync<AudioClip>(ResourcePath);
                _request.completed += OnClipLoaded;
                return;
            }

            if (!_source.isPlaying) _source.Play();
        }

        void OnClipLoaded(AsyncOperation operation)
        {
            if (this == null) return;
            var clip = ((ResourceRequest)operation).asset as AudioClip;
            if (clip == null)
            {
                Debug.LogWarning($"Couldn't load call ringtone from Resources/{ResourcePath}.");
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = 0.7f;
            _source.clip = clip;
            if (isActiveAndEnabled) Refresh();
        }
    }
}
