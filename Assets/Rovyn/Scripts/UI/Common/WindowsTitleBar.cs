#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Rovyn.UI
{
    internal static class WindowsTitleBar
    {
        const int ImmersiveDarkMode = 20;
        const int CaptionColor = 35;
        const int TextColor = 36;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (!Application.isBatchMode) ApplyAsync().Forget();
        }

        static async UniTaskVoid ApplyAsync()
        {
            uint processId = GetCurrentProcessId();
            string title = Application.productName;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                var window = FindPlayerWindow(processId, title);
                if (window != IntPtr.Zero)
                {
                    SetAttribute(window, ImmersiveDarkMode, 1);
                    if (SetAttribute(window, CaptionColor, 0x00161616) >= 0)
                        SetAttribute(window, TextColor, 0x00EEEEEE);
                    return;
                }

                await UniTask.Delay(100, ignoreTimeScale: true);
            }
        }

        static IntPtr FindPlayerWindow(uint processId, string title)
        {
            var window = IntPtr.Zero;
            while ((window = FindWindowExW(IntPtr.Zero, window, null, title)) != IntPtr.Zero)
            {
                GetWindowThreadProcessId(window, out uint ownerId);
                if (ownerId == processId) return window;
            }
            return IntPtr.Zero;
        }

        static int SetAttribute(IntPtr window, int attribute, int value) =>
            DwmSetWindowAttribute(window, attribute, ref value, sizeof(int));

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string className, string windowName);

        [DllImport("user32.dll", ExactSpelling = true)]
        static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        static extern uint GetCurrentProcessId();
    }
}
#endif
