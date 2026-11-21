using UnityEngine;

namespace Rovyn.UI
{
    // Dark palette used by the scripts and by the editor UI builder.
    public static class Theme
    {
        public static readonly Color Rail = Hex(0x141416);
        public static readonly Color Sidebar = Hex(0x121214);
        public static readonly Color Main = Hex(0x1A1A1E);
        public static readonly Color UserPanel = Hex(0x262628);
        public static readonly Color Input = Rail;
        public static readonly Color Composer = Hex(0x29292B);
        public static readonly Color Popup = Hex(0x252527);
        public static readonly Color Card = Sidebar;
        public static readonly Color Divider = Hex(0x333336);
        public static readonly Color ScrollThumb = Hex(0x505057);
        public static readonly Color Backdrop = new(0f, 0f, 0f, 0.7f);

        public static readonly Color Hover = Hex(0xFFFFFF, 0.06f);
        public static readonly Color Selected = Hex(0xD8D8DF, 0.13f);
        public static readonly Color MessageHover = Hex(0xFFFFFF, 0.04f);
        public static readonly Color InputSelection = Hex(0xC5C4CF, 0.28f);
        public static readonly Color Clear = new(0f, 0f, 0f, 0f);

        public static readonly Color TextHeader = Hex(0xEEEEEF);
        public static readonly Color TextNormal = Hex(0xDDDDDF);
        public static readonly Color TextMuted = Hex(0xA2A2A8);
        public static readonly Color TextLink = Hex(0xC3C0D3);
        public static readonly Color Interactive = Hex(0xB8B8BE);
        public static readonly Color InteractiveHover = TextNormal;
        public static readonly Color InteractiveActive = Hex(0xFFFFFF);

        public static readonly Color Brand = Hex(0x6B57F2);
        public static readonly Color Accent = Hex(0x51505B);
        public static readonly Color AccentHover = Hex(0x635F70);
        public static readonly Color AccentPressed = Hex(0x3D3B46);
        public static readonly Color Green = Hex(0x6CB38D);
        public static readonly Color GreenButton = Hex(0x355B49);
        public static readonly Color Yellow = Hex(0xC8A875);
        public static readonly Color Red = Hex(0xD77C80);
        public static readonly Color DangerButton = Hex(0x8D444D);
        public static readonly Color Grey = Hex(0x828287);
        public static readonly Color ErrorText = Hex(0xE39498);

        static readonly Color[] AvatarColors =
        {
            Brand, Hex(0x24814D), Hex(0xAD5C11), Hex(0xC13E4C), Hex(0xB53A81), Hex(0x3568CF), Hex(0x137C75),
        };

        public static Color StatusColor(UserStatus status) => status switch
        {
            UserStatus.Online => Green,
            UserStatus.Idle => Yellow,
            UserStatus.DoNotDisturb => Red,
            _ => Grey,
        };

        public static string StatusLabel(UserStatus status) => status switch
        {
            UserStatus.Online => "Online",
            UserStatus.Idle => "Idle",
            UserStatus.DoNotDisturb => "Do Not Disturb",
            UserStatus.Invisible => "Invisible",
            _ => "Offline",
        };

        // Stable color per player so placeholder avatars are recognizable.
        public static Color AvatarColor(string id)
        {
            if (string.IsNullOrEmpty(id)) return AvatarColors[0];
            unchecked
            {
                int hash = 17;
                foreach (var c in id) hash = hash * 31 + c;
                return AvatarColors[(hash & 0x7fffffff) % AvatarColors.Length];
            }
        }

        public static Color Hex(int rgb, float alpha = 1f) =>
            new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
