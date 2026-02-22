namespace ObjLoader.Core.Mmd
{
    public class PmxMorph
    {
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public byte Panel { get; set; }
        public byte MorphType { get; set; }
        public byte[] Offsets { get; set; } = Array.Empty<byte>();
    }
}