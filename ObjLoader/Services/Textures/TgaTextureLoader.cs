using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ObjLoader.Services.Textures
{
    public class TgaTextureLoader : ITextureLoader
    {
        public int Priority => 100;

        public bool CanLoad(string path)
        {
            return path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);
        }

        public BitmapSource Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            byte idLength = br.ReadByte();
            byte colorMapType = br.ReadByte();
            byte imageType = br.ReadByte();
            br.ReadBytes(2);
            ushort colorMapLength = br.ReadUInt16();
            byte colorMapEntrySize = br.ReadByte();
            br.ReadInt16();
            br.ReadInt16();
            short width = br.ReadInt16();
            short height = br.ReadInt16();
            byte pixelDepth = br.ReadByte();
            byte imageDescriptor = br.ReadByte();

            if (idLength > 0) br.ReadBytes(idLength);
            if (colorMapType == 1)
            {
                int skip = colorMapLength * colorMapEntrySize / 8;
                br.ReadBytes(skip);
            }

            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            int pixelCount = width * height;
            int currentPixel = 0;

            byte[] rawData = new byte[pixelCount * 4];
            int rawIdx = 0;

            if (imageType == 2)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    byte b = br.ReadByte();
                    byte g = br.ReadByte();
                    byte r = br.ReadByte();
                    byte a = pixelDepth == 32 ? br.ReadByte() : (byte)255;
                    rawData[rawIdx++] = b;
                    rawData[rawIdx++] = g;
                    rawData[rawIdx++] = r;
                    rawData[rawIdx++] = a;
                }
            }
            else if (imageType == 10)
            {
                while (currentPixel < pixelCount)
                {
                    byte header = br.ReadByte();
                    int count = (header & 0x7F) + 1;
                    if ((header & 0x80) != 0)
                    {
                        byte b = br.ReadByte();
                        byte g = br.ReadByte();
                        byte r = br.ReadByte();
                        byte a = pixelDepth == 32 ? br.ReadByte() : (byte)255;
                        for (int i = 0; i < count; i++)
                        {
                            rawData[rawIdx++] = b;
                            rawData[rawIdx++] = g;
                            rawData[rawIdx++] = r;
                            rawData[rawIdx++] = a;
                            currentPixel++;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            byte b = br.ReadByte();
                            byte g = br.ReadByte();
                            byte r = br.ReadByte();
                            byte a = pixelDepth == 32 ? br.ReadByte() : (byte)255;
                            rawData[rawIdx++] = b;
                            rawData[rawIdx++] = g;
                            rawData[rawIdx++] = r;
                            rawData[rawIdx++] = a;
                            currentPixel++;
                        }
                    }
                }
            }

            bool isTopLeft = (imageDescriptor & 0x20) != 0;
            if (!isTopLeft)
            {
                for (int y = 0; y < height; y++)
                {
                    Array.Copy(rawData, y * stride, pixels, (height - 1 - y) * stride, stride);
                }
            }
            else
            {
                Array.Copy(rawData, pixels, rawData.Length);
            }

            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        }
    }
}