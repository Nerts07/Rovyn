using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Vivox;

namespace Rovyn.UI
{
    internal sealed class ReactionHistory
    {
        readonly Dictionary<string, Dictionary<(string emoji, string player), Reaction>> _messages = new();

        readonly struct Reaction
        {
            internal readonly DateTime Time;
            internal readonly string Id;
            internal readonly bool Added;

            internal Reaction(VivoxMessage message, bool added)
            {
                Time = message.ReceivedTime;
                Id = message.MessageId;
                Added = added;
            }
        }

        internal void Clear() => _messages.Clear();

        internal string Apply(VivoxMessage message)
        {
            var data = ChatMetadata.ReadReaction(message);
            string emoji = EmojiCatalog.Canonical(data?.emoji);
            if (emoji == null || string.IsNullOrEmpty(message.SenderPlayerId) || string.IsNullOrEmpty(message.MessageId))
                return null;

            if (!_messages.TryGetValue(data.target, out var reactions))
                _messages[data.target] = reactions = new();

            var key = (emoji, message.SenderPlayerId);
            if (reactions.TryGetValue(key, out var previous) &&
                (previous.Time > message.ReceivedTime ||
                 (previous.Time == message.ReceivedTime && string.CompareOrdinal(previous.Id, message.MessageId) >= 0)))
                return null;

            reactions[key] = new Reaction(message, data.added);
            return data.target;
        }

        internal bool Has(string target, string emoji, string playerId) =>
            _messages.TryGetValue(target, out var reactions) &&
            reactions.TryGetValue((emoji, playerId), out var reaction) && reaction.Added;

        internal IEnumerable<(string emoji, int count, bool own)> For(string target, string playerId)
        {
            if (!_messages.TryGetValue(target, out var reactions)) yield break;
            foreach (var group in reactions.Where(item => item.Value.Added).GroupBy(item => item.Key.emoji)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
                yield return (group.Key, group.Count(), group.Any(item => item.Key.player == playerId));
        }
    }
}
