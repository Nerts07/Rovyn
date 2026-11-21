using System;
using Unity.Services.Vivox;
using UnityEngine;

namespace Rovyn
{
    [Serializable]
    internal sealed class ChatMetadata
    {
        const string ReplyPrefix = "rovyn/reply/1:";
        const string ReplyTrailer = "\n\u2063rvq1:";
        const string ReactionAdded = "\u2063rv+1:";
        const string ReactionRemoved = "\u2063rv-1:";
        const string Marker = "\u2063rv";
        const string EscapedMarker = "\u2063\u2063rv";

        public string target;
        public string author;
        public string excerpt;
        public string emoji;
        public bool added;

        internal MessageOptions AsReply() => new() { Metadata = ReplyPrefix + JsonUtility.ToJson(this) };
        // Vivox 16.13 preserves the message body in history, but omits live metadata from archive records.
        internal static string EscapeBody(string body) => body?.Replace(Marker, EscapedMarker);
        internal string AddReplyTrailer(string body) => EscapeBody(body) + ReplyTrailer + target;
        internal static string ReactionBody(string target, string emoji, bool added) =>
            (added ? ReactionAdded : ReactionRemoved) + target + "\n" + emoji;

        internal static bool IsReaction(VivoxMessage message) => ReadReaction(message) != null;

        internal static ChatMetadata ReadReaction(VivoxMessage message)
        {
            var body = message.MessageText;
            if (string.IsNullOrEmpty(body) || body.Length > 330) return null;
            bool added = body.StartsWith(ReactionAdded, StringComparison.Ordinal);
            if (!added && !body.StartsWith(ReactionRemoved, StringComparison.Ordinal)) return null;
            int start = ReactionAdded.Length;
            int separator = body.IndexOf('\n', start);
            int emojiLength = body.Length - separator - 1;
            if (separator <= start || emojiLength is < 1 or > 64) return null;
            var target = body.Substring(start, separator - start);
            return IsTarget(target)
                ? new ChatMetadata { target = target, added = added, emoji = body.Substring(separator + 1) }
                : null;
        }

        internal static ChatMetadata ReadReply(VivoxMessage message, out string body)
        {
            body = message.MessageText ?? "";
            int marker = body.LastIndexOf(ReplyTrailer, StringComparison.Ordinal);
            var target = marker < 0 ? null : body.Substring(marker + ReplyTrailer.Length);
            if (!IsTarget(target))
            {
                body = body.Replace(EscapedMarker, Marker);
                return null;
            }
            body = body.Substring(0, marker).Replace(EscapedMarker, Marker);
            var live = Read(message.Metadata, ReplyPrefix);
            return live?.target == target ? live : new ChatMetadata { target = target };
        }

        static bool IsTarget(string value) =>
            value is { Length: > 0 and <= 256 } && value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0;

        static ChatMetadata Read(string metadata, string prefix)
        {
            if (metadata == null || metadata.Length > 4096 || !metadata.StartsWith(prefix, StringComparison.Ordinal))
                return null;

            try
            {
                var value = JsonUtility.FromJson<ChatMetadata>(metadata.Substring(prefix.Length));
                return IsTarget(value?.target) ? value : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        internal static string Preview(string value, int limit)
        {
            var line = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (line.Length <= limit) return line;
            if (char.IsHighSurrogate(line[limit - 1])) limit--;
            return line.Substring(0, limit) + "…";
        }
    }
}
