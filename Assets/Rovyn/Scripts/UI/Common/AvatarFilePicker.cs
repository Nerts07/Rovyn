using System;
using Cysharp.Threading.Tasks;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
#endif

namespace Rovyn.UI
{
    internal static class AvatarFilePicker
    {
        internal static bool IsSupported =>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            true;
#else
            false;
#endif

        internal static async UniTask<byte[]> PickAsync()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var window = GetActiveWindow();
            var completion = new UniTaskCompletionSource<byte[]>();
            var thread = new Thread(() =>
            {
                try { completion.TrySetResult(PickImage(window)); }
                catch (Exception error) { completion.TrySetException(error); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            try { return await completion.Task; }
            finally { await UniTask.SwitchToMainThread(); }
#else
            await UniTask.CompletedTask;
            throw new PlatformNotSupportedException("Avatar uploads are available in Rovyn for Windows.");
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const int PathCapacity = 32768;
        const int Explorer = 0x00080000;
        const int NoChangeDirectory = 0x00000008;
        const int FileMustExist = 0x00001000;

        static byte[] PickImage(IntPtr window)
        {
            var pathBuffer = Marshal.AllocHGlobal(PathCapacity * sizeof(char));
            var filter = IntPtr.Zero;
            try
            {
                Marshal.WriteInt16(pathBuffer, 0);
                filter = Marshal.StringToHGlobalUni("PNG and JPEG images\0*.png;*.jpg;*.jpeg\0\0");
                var options = new OpenFileName
                {
                    Size = Marshal.SizeOf<OpenFileName>(),
                    Owner = window,
                    Filter = filter,
                    FilterIndex = 1,
                    File = pathBuffer,
                    MaxFile = PathCapacity,
                    Title = "Choose a public avatar",
                    Flags = Explorer | NoChangeDirectory | FileMustExist,
                };
                if (!GetOpenFileNameW(ref options))
                {
                    if (CommDlgExtendedError() == 0) return null;
                    throw new InvalidOperationException("Couldn't open the file picker.");
                }
                return ReadImage(Marshal.PtrToStringUni(pathBuffer));
            }
            finally
            {
                Marshal.FreeHGlobal(pathBuffer);
                if (filter != IntPtr.Zero) Marshal.FreeHGlobal(filter);
            }
        }

        static byte[] ReadImage(string path)
        {
            try
            {
                using var image = new BinaryReader(File.OpenRead(path));
                long size = image.BaseStream.Length;
                if (size > AvatarImage.MaxInputBytes)
                    throw new InvalidOperationException("Choose an image under 10 MB.");
                return image.ReadBytes((int)size);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException("Couldn't read the selected image.");
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            internal int Size;
            internal IntPtr Owner, Instance, Filter, CustomFilter;
            internal int MaxCustomFilter, FilterIndex;
            internal IntPtr File;
            internal int MaxFile;
            internal IntPtr FileTitle;
            internal int MaxFileTitle;
            internal IntPtr InitialDirectory;
            [MarshalAs(UnmanagedType.LPWStr)] internal string Title;
            internal int Flags;
            internal ushort FileOffset, FileExtension;
            internal IntPtr DefaultExtension, CustomData, Hook, TemplateName, Reserved;
            internal int ReservedFlags, FlagsEx;
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetActiveWindow();

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetOpenFileNameW(ref OpenFileName options);

        [DllImport("comdlg32.dll", ExactSpelling = true)]
        static extern uint CommDlgExtendedError();
#endif
    }
}
