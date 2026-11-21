using System;
using System.Text;
using UnityEngine;

namespace Rovyn
{
    // The servers and channels shown in the app. UGS has no shared "guild" storage that clients
    // can safely write to, so servers are defined here in the project and edited in the Inspector.
    // Anyone who runs the app can join these channels; see the README notes on access control.
    [CreateAssetMenu(menuName = "Rovyn/Server Catalog", fileName = "ServerCatalog")]
    public class ServerCatalog : ScriptableObject
    {
        [Serializable]
        public class Server
        {
            public string id = "lounge";
            public string name = "Rovyn Lounge";
            public string[] textChannels = { "general" };
            public string[] voiceChannels = { "Lounge" };
        }

        public Server[] servers = { new Server() };

        public static string TextChannelId(Server server, string channel) => $"srv.{Slug(server.id)}.t.{Slug(channel)}";
        public static string VoiceChannelId(Server server, string channel) => $"srv.{Slug(server.id)}.v.{Slug(channel)}";

        // Vivox channel names are kept to [a-z0-9._-].
        static string Slug(string value)
        {
            var sb = new StringBuilder();
            foreach (var c in value.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_') sb.Append(c);
                else if (char.IsWhiteSpace(c)) sb.Append('-');
            }
            return sb.Length > 0 ? sb.ToString() : "x";
        }
    }
}
