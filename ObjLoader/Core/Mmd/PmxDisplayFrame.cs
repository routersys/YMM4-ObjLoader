namespace ObjLoader.Core.Mmd
{
    public class PmxDisplayFrame
    {
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public byte SpecialFlag { get; set; }
        public List<PmxDisplayElement> Elements { get; set; } = new List<PmxDisplayElement>();
    }

    public class PmxDisplayElement
    {
        public byte ElementType { get; set; }
        public int Index { get; set; }
    }
}