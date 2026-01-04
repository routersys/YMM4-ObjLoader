using System.IO;
using System.Text;

namespace ObjLoader.Utilities
{
    public static class EncodingUtil
    {
        public static string ReadAllText(string path)
        {
            if (!File.Exists(path)) return string.Empty;

            var bytes = File.ReadAllBytes(path);
            var encoding = DetectEncoding(bytes);
            var text = encoding.GetString(bytes);

            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            text = text.Replace("\0", "");

            return text;
        }

        private static Encoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8;
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode;
            }

            if (IsUtf8(bytes))
            {
                return Encoding.UTF8;
            }

            try
            {
                return Encoding.GetEncoding(932);
            }
            catch
            {
                return Encoding.Default;
            }
        }

        private static bool IsUtf8(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b = bytes[i];
                if (b < 0x80)
                {
                    i++;
                    continue;
                }

                int expected = 0;
                if ((b & 0xE0) == 0xC0) expected = 1;
                else if ((b & 0xF0) == 0xE0) expected = 2;
                else if ((b & 0xF8) == 0xF0) expected = 3;
                else return false;

                i++;
                for (int j = 0; j < expected; j++)
                {
                    if (i >= bytes.Length) return false;
                    if ((bytes[i] & 0xC0) != 0x80) return false;
                    i++;
                }
            }
            return true;
        }
    }
}