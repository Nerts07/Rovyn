using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace Rovyn
{
    internal sealed class AvatarService
    {
        const string Endpoint = "https://cloud-save.services.api.unity.com/v1/data/projects";
        const string Key = "rovyn_avatar";
        const int RefreshSeconds = 60;

        sealed class Entry
        {
            internal Texture2D Texture;
            internal string Data;
            internal double NextRead;
            internal bool Reading;
            internal bool Writing;
            internal int Revision;
            internal Action<Texture2D> Changed;
        }

        readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        readonly SemaphoreSlim _reads = new(4, 4);
        int _session;

        internal void Start() => RefreshVisibleAsync(++_session).Forget();

        internal void Stop()
        {
            _session++;
            foreach (var entry in _entries.Values)
            {
                entry.Changed?.Invoke(null);
                if (entry.Texture != null) UnityEngine.Object.Destroy(entry.Texture);
            }
            _entries.Clear();
        }

        internal Texture2D Watch(string player, Action<Texture2D> changed)
        {
            var entry = GetEntry(player);
            entry.Changed += changed;
            Refresh(player, entry);
            return entry.Texture;
        }

        internal void Unwatch(string player, Action<Texture2D> changed)
        {
            if (_entries.TryGetValue(player, out var entry)) entry.Changed -= changed;
        }

        internal async UniTask SaveAsync(byte[] image)
        {
            if (!App.SessionActive || !App.Auth.IsSignedIn)
                throw new InvalidOperationException("Sign in to change your avatar.");

            string player = App.Auth.PlayerId;
            var entry = GetEntry(player);
            if (entry.Writing) return;
            string data = image == null ? null : AvatarImage.Prepare(image);
            var texture = AvatarImage.Decode(data);
            if (image != null && texture == null)
                throw new InvalidOperationException("Couldn't prepare this image. Try another PNG or JPEG.");

            int session = _session;
            entry.Writing = true;
            entry.Revision++;
            try
            {
                using var request = data == null
                    ? UnityWebRequest.Delete($"{ItemsUrl(player)}/{Key}")
                    : UnityWebRequest.Post(ItemsUrl(player), new JObject { ["key"] = Key, ["value"] = data }
                        .ToString(Formatting.None), "application/json");
                Configure(request);
                try { await request.SendWebRequest(); }
                catch (UnityWebRequestException) when (data == null && request.responseCode == 404) { }
                catch (UnityWebRequestException)
                {
                    throw new InvalidOperationException(request.responseCode switch
                    {
                        401 => "Sign in again to change your avatar.",
                        403 or 404 => "Avatar uploads are unavailable. Check Cloud Save in Unity Dashboard.",
                        429 => "Too many requests. Try again in a moment.",
                        _ => "Couldn't save your avatar. Check your connection and try again.",
                    });
                }

                if (session != _session || player != App.Auth.PlayerId) throw new OperationCanceledException();
                entry.NextRead = Time.realtimeSinceStartupAsDouble + RefreshSeconds;
                SetImage(entry, data, texture);
                texture = null;
            }
            finally
            {
                entry.Writing = false;
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }

        Entry GetEntry(string player)
        {
            if (!_entries.TryGetValue(player, out var entry)) _entries.Add(player, entry = new Entry());
            return entry;
        }

        void Refresh(string player, Entry entry)
        {
            if (!App.SessionActive || !App.Auth.IsSignedIn || entry.Reading || entry.Writing ||
                Time.realtimeSinceStartupAsDouble < entry.NextRead) return;
            entry.NextRead = Time.realtimeSinceStartupAsDouble + RefreshSeconds;
            entry.Reading = true;
            FetchAsync(player, entry, _session, entry.Revision).Forget();
        }

        async UniTaskVoid RefreshVisibleAsync(int session)
        {
            while (session == _session)
            {
                foreach (var (player, entry) in _entries.ToArray())
                {
                    if (entry.Changed != null) Refresh(player, entry);
                    else if (!entry.Reading && !entry.Writing && Time.realtimeSinceStartupAsDouble >= entry.NextRead + RefreshSeconds)
                    {
                        if (entry.Texture != null) UnityEngine.Object.Destroy(entry.Texture);
                        _entries.Remove(player);
                    }
                }
                await UniTask.Delay(TimeSpan.FromSeconds(15), ignoreTimeScale: true);
            }
        }

        async UniTaskVoid FetchAsync(string player, Entry entry, int session, int revision)
        {
            await _reads.WaitAsync();
            try
            {
                if (session != _session || entry.Revision != revision) return;
                if (entry.Changed == null) { entry.NextRead = 0; return; }

                using var request = UnityWebRequest.Get($"{ItemsUrl(player)}?keys={Key}");
                Configure(request);
                try { await request.SendWebRequest(); }
                catch (UnityWebRequestException) when (request.responseCode == 404)
                {
                    if (session == _session && entry.Revision == revision) SetImage(entry, null, null);
                    return;
                }
                if (session != _session || entry.Revision != revision) return;

                string data = null;
                if (request.downloadedBytes <= AvatarImage.MaxEncodedLength + 4096)
                {
                    var items = JObject.Parse(request.downloadHandler.text)["results"] as JArray;
                    var value = items?.FirstOrDefault(item => (string)item["key"] == Key)?["value"];
                    if (value?.Type == JTokenType.String) data = (string)value;
                }
                if (data != entry.Data) SetImage(entry, data, AvatarImage.Decode(data));
            }
            catch (UnityWebRequestException) { } // Keep the current avatar until a later refresh succeeds.
            catch (JsonException e) { Debug.LogWarning($"Couldn't read an avatar: {e.Message}"); }
            finally
            {
                entry.Reading = false;
                _reads.Release();
            }
        }

        static void SetImage(Entry entry, string data, Texture2D texture)
        {
            var old = entry.Texture;
            entry.Texture = texture;
            entry.Data = data;
            if (old != texture) entry.Changed?.Invoke(texture);
            if (old != null) UnityEngine.Object.Destroy(old);
        }

        static string ItemsUrl(string player) =>
            $"{Endpoint}/{Application.cloudProjectId}/players/{UnityWebRequest.EscapeURL(player)}/public/items";

        static void Configure(UnityWebRequest request)
        {
            request.timeout = 15;
            request.SetRequestHeader("Authorization", "Bearer " + AuthenticationService.Instance.AccessToken);
        }
    }
}
