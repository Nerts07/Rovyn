using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace Rovyn.UI
{
    internal static class EmojiCatalog
    {
        const int SpriteCodeStart = 0xE000;
        const int AtlasSize = 1024;
        const int CellSize = 32;
        const int PerSide = AtlasSize / CellSize;
        const int PerAtlas = PerSide * PerSide;

        internal sealed class Entry
        {
            internal string Text;
            internal string Name;
            internal string Group;
            internal int Index;
        }

        static readonly List<Entry> Items = new();
        static readonly Dictionary<string, Entry> Lookup = new(StringComparer.Ordinal);
        static readonly HashSet<char> LeadingCharacters = new();
        static int _longest;
        static bool _loaded;
        static bool _spriteInitializationFailed;
        static TMP_SpriteAsset _sprites;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSpriteCache()
        {
            _sprites = null;
            _spriteInitializationFailed = false;
        }

        internal static IReadOnlyList<Entry> Entries
        {
            get { Load(); return Items; }
        }

        internal static string Canonical(string text)
        {
            Load();
            return text != null && Lookup.TryGetValue(text, out var entry) ? entry.Text : null;
        }

        static bool TryAttach(TMP_Text label)
        {
            if (_sprites == null && !_spriteInitializationFailed)
            {
                try
                {
                    _sprites = CreateSprites();
                }
                catch (Exception error)
                {
                    _spriteInitializationFailed = true;
                    Debug.LogWarning($"Couldn't load color emoji: {error.Message}");
                }
            }
            if (_sprites == null) return false;
            if (label.spriteAsset != _sprites) label.spriteAsset = _sprites;
            return true;
        }

        internal static void AttachComposer(TMP_Text label, string text)
        {
            if (label.textPreprocessor != null || _spriteInitializationFailed || string.IsNullOrEmpty(text)) return;
            if (!string.Equals(text, Format(text), StringComparison.Ordinal) && TryAttach(label))
                label.textPreprocessor = new ComposerEmoji();
        }

        sealed class ComposerEmoji : ITextPreprocessor
        {
            // Preserve UTF-16 offsets so the input field can edit and copy the original Unicode text.
            public string PreprocessText(string text) => Format(text, preserveOffsets: true);
        }

        internal static void SetText(TMP_Text label, string text)
        {
            text ??= "";
            var formatted = _spriteInitializationFailed ? text : Format(text);
            label.text = !string.Equals(text, formatted, StringComparison.Ordinal) && TryAttach(label) ? formatted : text;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            var data = Resources.Load<TextAsset>("Emoji/emoji-test");
            if (data == null) return;

            string group = "";
            var variants = new List<(string text, string name)>();
            var names = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var rawLine in data.text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("# group: ", StringComparison.Ordinal))
                {
                    group = line.Substring(9);
                    continue;
                }
                if (line.Length == 0 || line[0] == '#') continue;

                int semicolon = line.IndexOf(';');
                int comment = line.IndexOf('#', semicolon + 1);
                if (semicolon <= 0 || comment <= semicolon) continue;

                var status = line.Substring(semicolon + 1, comment - semicolon - 1).Trim();
                if (status != "fully-qualified" && status != "minimally-qualified" && status != "unqualified") continue;
                var words = line.Substring(comment + 1).Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 3) continue;

                var unicode = new StringBuilder();
                foreach (var codepoint in line.Substring(0, semicolon).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(codepoint, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value) ||
                        value < 0 || value > 0x10FFFF || value is >= 0xD800 and <= 0xDFFF)
                    {
                        unicode.Clear();
                        break;
                    }
                    unicode.Append(char.ConvertFromUtf32(value));
                }
                if (unicode.Length == 0) continue;
                var text = unicode.ToString();
                var name = words[2];
                if (status != "fully-qualified")
                {
                    variants.Add((text, name));
                    continue;
                }

                var entry = new Entry { Text = text, Name = name, Group = group, Index = Items.Count };
                Items.Add(entry);
                names.TryAdd(name, entry);
                Register(text, entry);
            }
            foreach (var (text, name) in variants)
                if (names.TryGetValue(name, out var entry)) Register(text, entry);
        }

        static void Register(string text, Entry entry)
        {
            Lookup.TryAdd(text, entry);
            LeadingCharacters.Add(text[0]);
            _longest = Math.Max(_longest, text.Length);
        }

        static string Format(string text, bool preserveOffsets = false)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            Load();
            StringBuilder result = null;
            for (int i = 0; i < text.Length;)
            {
                int length = LeadingCharacters.Contains(text[i]) ? Math.Min(text.Length - i, _longest) : 0;
                Entry entry = null;
                while (length > 0 && !Lookup.TryGetValue(text.Substring(i, length), out entry)) length--;

                if (entry == null || (i + length < text.Length && text[i + length] == '\uFE0E'))
                {
                    result?.Append(text[i]);
                    i++;
                    continue;
                }
                if (result == null) result = new StringBuilder(text.Length).Append(text, 0, i);
                result.Append((char)(SpriteCodeStart + entry.Index));
                if (preserveOffsets) result.Append('\u200B', length - 1);
                i += length;
            }
            return result == null ? text : result.ToString();
        }

        static TMP_SpriteAsset CreateSprites()
        {
            var template = TMP_Settings.defaultSpriteAsset;
            if (template == null || template.material == null || string.IsNullOrEmpty(template.version))
                throw new InvalidOperationException("TextMeshPro's default sprite asset is unavailable.");

            const int expectedEmoji = 3944;
            if (Items.Count != expectedEmoji)
                throw new InvalidOperationException("The Unicode emoji list does not match the bundled atlases.");

            var textures = new List<Texture2D>();
            for (int start = 0; start < Items.Count; start += PerAtlas)
            {
                var texture = Resources.Load<Texture2D>($"Emoji/Atlas{start / PerAtlas}");
                if (texture == null || texture.width != AtlasSize || texture.height != AtlasSize)
                    throw new InvalidOperationException($"Emoji atlas {start / PerAtlas} is missing or has the wrong size.");
                textures.Add(texture);
            }

            var pages = new List<TMP_SpriteAsset>();
            for (int atlas = 0; atlas < textures.Count; atlas++)
            {
                // The template carries TMP's serialized version, so the new tables never enter legacy migration.
                var page = UnityEngine.Object.Instantiate(template);
                var texture = textures[atlas];
                page.name = $"Rovyn Emoji {atlas + 1}";
                page.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
                page.spriteSheet = texture;
                page.material = new Material(template.material) { mainTexture = texture };
                var characters = page.spriteCharacterTable;
                characters.Clear();
                page.spriteGlyphTable.Clear();

                int start = atlas * PerAtlas;
                for (int index = start; index < Math.Min(start + PerAtlas, Items.Count); index++)
                {
                    int local = index - start;
                    int x = local % PerSide * CellSize;
                    int y = AtlasSize - (local / PerSide + 1) * CellSize;
                    var glyph = new TMP_SpriteGlyph((uint)local + 1, new GlyphMetrics(32, 32, 0, 26, 32),
                        new GlyphRect(x, y, CellSize, CellSize), 1f, 0);
                    page.spriteGlyphTable.Add(glyph);
                    characters.Add(new TMP_SpriteCharacter((uint)(SpriteCodeStart + index), page, glyph)
                        { name = $"rovyn-emoji-{index}" });
                    var sequence = Items[index].Text;
                    int first = char.ConvertToUtf32(sequence, 0);
                    int firstLength = first > 0xFFFF ? 2 : 1;
                    if (sequence.Length == firstLength ||
                        (sequence.Length == firstLength + 1 && sequence[firstLength] == '\uFE0F'))
                        characters.Add(new TMP_SpriteCharacter((uint)first, page, glyph)
                            { name = $"rovyn-unicode-{index}" });
                }
                page.UpdateLookupTables();
                pages.Add(page);
            }
            var root = pages[0];
            pages.RemoveAt(0);
            root.fallbackSpriteAssets = pages;
            return root;
        }
    }
}
