using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rovyn
{
    internal static class CallHistory
    {
        [Serializable]
        internal sealed class Entry
        {
            public string id;
            public string caller;
            public long endedAt;
            public int seconds;

            internal DateTime EndedAtUtc => DateTimeOffset.FromUnixTimeMilliseconds(endedAt).UtcDateTime;
        }

        [Serializable]
        sealed class History
        {
            public List<Entry> calls = new();
        }

        internal static IReadOnlyList<Entry> Read(string player, string peer) => Load(Key(player, peer)).calls;

        internal static void Record(string player, string peer, string id, string caller, int seconds)
        {
            var key = Key(player, peer);
            var history = Load(key);
            if (history.calls.Exists(call => call.id == id)) return;
            history.calls.Add(new Entry
            {
                id = id,
                caller = caller,
                endedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                seconds = seconds,
            });
            PlayerPrefs.SetString(key, JsonUtility.ToJson(history));
            PlayerPrefs.Save();
        }

        static History Load(string key)
        {
            var json = PlayerPrefs.GetString(key);
            return string.IsNullOrEmpty(json) ? new History() : JsonUtility.FromJson<History>(json) ?? new History();
        }

        static string Key(string player, string peer) => $"rovyn.call_history.{player}.{peer}";
    }
}
