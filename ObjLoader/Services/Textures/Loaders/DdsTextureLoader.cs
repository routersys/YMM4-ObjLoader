using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ObjLoader.Localization;
using ObjLoader.Utilities;

namespace ObjLoader.Services.Textures.Loaders
{
    public class DdsTextureLoader : ITextureLoader
    {
        public int Priority => 90;

        public bool CanLoad(string path)
        {
            return path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase);
        }

        public bool CanLoadRaw(string path)
        {
            return path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase);
        }

        public BitmapSource Load(string path)
        {
            using var raw = LoadRaw(path);
            var bmp = BitmapSource.Create(raw.Width, raw.Height, 96, 96, PixelFormats.Bgra32, null, raw.Pixels, raw.Stride);
            if (bmp.CanFreeze) bmp.Freeze();
            return bmp;
        }

        public TextureRawData LoadRaw(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            uint magic = br.ReadUInt32();
            if (magic != 0x20534444)
            {
                UserNotification.ShowError(Texts.Error_DdsInvalidMagic, Texts.Title_LoadError);
                throw new InvalidDataException("Invalid DDS Magic");
            }

            uint size = br.ReadUInt32();
            if (size != 124)
            {
                UserNotification.ShowError(Texts.Error_DdsInvalidHeaderSize, Texts.Title_LoadError);
                throw new InvalidDataException("Invalid DDS Header Size");
            }

            uint flags = br.ReadUInt32();
            uint height = br.ReadUInt32();
            uint width = br.ReadUInt32();
            uint pitchOrLinearSize = br.ReadUInt32();
            uint depth = br.ReadUInt32();
            uint mipMapCount = br.ReadUInt32();
            br.ReadBytes(44);

            uint pfSize = br.ReadUInt32();
            uint pfFlags = br.ReadUInt32();
            uint pfFourCC = br.ReadUInt32();
            uint pfRGBBitCount = br.ReadUInt32();
            uint pfRBitMask = br.ReadUInt32();
            uint pfGBitMask = br.ReadUInt32();
            uint pfBBitMask = br.ReadUInt32();
            uint pfABitMask = br.ReadUInt32();

            br.ReadBytes(16);
            br.ReadBytes(4);

            bool isCompressed = (pfFlags & 0x4) != 0;
            bool isRgb = (pfFlags & 0x40) != 0;

            if (isCompressed)
            {
                string fourCCString = System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(pfFourCC));
                UserNotification.ShowError(string.Format(Texts.Error_DdsCompressedNotSupported, fourCCString), Texts.Title_LoadError);
                throw new NotSupportedException($"Compressed DDS not supported: {fourCCString}");
            }

            if (!isRgb)
            {
                UserNotification.ShowError(Texts.Error_DdsFormatNotSupported, Texts.Title_LoadError);
                throw new NotSupportedException("Only RGB/RGBA DDS supported");
            }

            if (pfRGBBitCount != 32 && pfRGBBitCount != 24)
            {
                UserNotification.ShowError(Texts.Error_DdsBitDepthNotSupported, Texts.Title_LoadError);
                throw new NotSupportedException($"DDS BitCount {pfRGBBitCount} not supported");
            }

            if (width == 0 || height == 0 || width > 65536 || height > 65536)
            {
                UserNotification.ShowError(Texts.Error_DdsDataReadFailed, Texts.Title_LoadError);
                throw new InvalidDataException($"Invalid DDS dimensions: {width}x{height}");
            }

            int rShift = GetShift(pfRBitMask);
            int gShift = GetShift(pfGBitMask);
            int bShift = GetShift(pfBBitMask);
            int aShift = GetShift(pfABitMask);

            int w = (int)width;
            int h = (int)height;
            int bytesPerPixel = (int)(pfRGBBitCount / 8);

            bool hasPitchFlag = (flags & 0x8) != 0;
            int fileStride = hasPitchFlag && pitchOrLinearSize > 0
                ? (int)pitchOrLinearSize
                : w * bytesPerPixel;

            int minStride = w * bytesPerPixel;
            if (fileStride < minStride) fileStride = minStride;
            int padding = fileStride - minStride;

            long requiredFileBytes = (long)fileStride * h;
            long availableBytes = fs.Length - fs.Position;
            if (availableBytes < requiredFileBytes)
            {
                UserNotification.ShowError(Texts.Error_DdsDataReadFailed, Texts.Title_LoadError);
                throw new InvalidDataException("DDS file is truncated");
            }

            TextureRawData? rawData = null;

            try
            {
                rawData = new TextureRawData(w, h);
                byte[] pixels = rawData.Pixels;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        uint pixelVal;
                        if (bytesPerPixel == 3)
                        {
                            byte b0 = br.ReadByte();
                            byte b1 = br.ReadByte();
                            byte b2 = br.ReadByte();
                            pixelVal = (uint)(b0 | (b1 << 8) | (b2 << 16));
                        }
                        else
                        {
                            pixelVal = br.ReadUInt32();
                        }

                        byte r = (byte)((pixelVal & pfRBitMask) >> rShift);
                        byte g = (byte)((pixelVal & pfGBitMask) >> gShift);
                        byte b = (byte)((pixelVal & pfBBitMask) >> bShift);
                        byte a = pfABitMask != 0 ? (byte)((pixelVal & pfABitMask) >> aShift) : (byte)255;

                        int destIdx = (y * w + x) * 4;
                        pixels[destIdx] = b;
                        pixels[destIdx + 1] = g;
                        pixels[destIdx + 2] = r;
                        pixels[destIdx + 3] = a;
                    }

                    if (padding > 0)
                    {
                        br.BaseStream.Seek(padding, SeekOrigin.Current);
                    }
                }

                return rawData;
            }
            catch (Exception ex)
            {
                rawData?.Dispose();
                UserNotification.ShowError(Texts.Error_DdsDataReadFailed, Texts.Title_LoadError);
                throw new InvalidDataException("Failed to read DDS pixel data", ex);
            }
        }

        private static int GetShift(uint mask)
        {
            if (mask == 0) return 0;
            int shift = 0;
            while ((mask & 1) == 0)
            {
                mask >>= 1;
                shift++;
            }
            return shift;
        }
    }
}