using System;
using System.Buffers.Binary;
using UnityEngine;

namespace Rovyn
{
    internal static class AvatarImage
    {
        const int Size = 128;
        internal const int MaxInputBytes = 10 * 1024 * 1024;
        internal const int MaxEncodedLength = 96 * 1024;
        static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        internal static string Prepare(byte[] bytes)
        {
            var (width, height) = PngSize(bytes);
            if (width == 0) (width, height) = JpegSize(bytes);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Choose a valid PNG or JPEG image.");
            if (width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize || (long)width * height > 32_000_000)
                throw new InvalidOperationException("Choose an image under 32 megapixels.");

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(bytes, markNonReadable: true))
                    throw new InvalidOperationException("Couldn't open this image. Try another PNG or JPEG.");
                return Crop(source);
            }
            finally { UnityEngine.Object.Destroy(source); }
        }

        static string Crop(Texture2D source)
        {
            var target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var thumbnail = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                float edge = Mathf.Min(source.width, source.height);
                var scale = new Vector2(edge / source.width, edge / source.height);
                Graphics.Blit(source, target, scale, (Vector2.one - scale) * 0.5f);
                RenderTexture.active = target;
                thumbnail.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
                return Convert.ToBase64String(thumbnail.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(thumbnail);
            }
        }

        internal static Texture2D Decode(string data)
        {
            if (string.IsNullOrEmpty(data) || data.Length > MaxEncodedLength) return null;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); }
            catch (FormatException) { return null; }

            if (PngSize(bytes) != (Size, Size)) return null;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true);
            if (texture.LoadImage(bytes))
            {
                ApplyCircularAlpha(texture);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Trilinear;
                texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                return texture;
            }
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        // Keep the source colors and soften only the outer pixel of the circular crop.
        static void ApplyCircularAlpha(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            const float center = (Size - 1) * 0.5f;
            const float radius = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                float dy = y - center;
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - center;
                    float coverage = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    int index = y * Size + x;
                    pixels[index].a = (byte)Mathf.RoundToInt(pixels[index].a * coverage);
                }
            }
            texture.SetPixels32(pixels);
        }

        static (int width, int height) PngSize(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 33 || !bytes.Slice(0, 8).SequenceEqual(PngSignature) ||
                BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(8, 4)) != 13 ||
                BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(12, 4)) != 0x49484452) return default;
            return (BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4)),
                BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4)));
        }

        static (int width, int height) JpegSize(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8) return default;
            int i = 2;
            while (i < bytes.Length && bytes[i++] == 0xff)
            {
                while (i < bytes.Length && bytes[i] == 0xff) i++;
                if (i + 2 >= bytes.Length) break;
                byte marker = bytes[i++];
                if (marker is 0xda or 0xd9) break;
                int length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(i, 2));
                if (length < 2 || length > bytes.Length - i) break;
                if ((marker is 0xc0 or 0xc1 or 0xc2) && length >= 8)
                    return (BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(i + 5, 2)),
                        BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(i + 3, 2)));
                i += length;
            }
            return default;
        }
    }
}
