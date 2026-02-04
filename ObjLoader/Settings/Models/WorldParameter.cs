namespace ObjLoader.Settings
{
    public class WorldParameter : ICloneable
    {
        public LightingSettings Lighting { get; set; } = new();
        public ToonSettings Toon { get; set; } = new();
        public RimSettings Rim { get; set; } = new();
        public OutlineSettings Outline { get; set; } = new();
        public FogSettings Fog { get; set; } = new();
        public PostEffectSettings PostEffect { get; set; } = new();
        public VignetteSettings Vignette { get; set; } = new();
        public ScanlineSettings Scanline { get; set; } = new();
        public ArtisticSettings Artistic { get; set; } = new();
        public PbrSettings PBR { get; set; } = new();
        public SsrSettings SSR { get; set; } = new();
        public PcssSettings PCSS { get; set; } = new();

        public object Clone()
        {
            var clone = (WorldParameter)MemberwiseClone();
            clone.Lighting = (LightingSettings)Lighting.Clone();
            clone.Toon = (ToonSettings)Toon.Clone();
            clone.Rim = (RimSettings)Rim.Clone();
            clone.Outline = (OutlineSettings)Outline.Clone();
            clone.Fog = (FogSettings)Fog.Clone();
            clone.PostEffect = (PostEffectSettings)PostEffect.Clone();
            clone.Vignette = (VignetteSettings)Vignette.Clone();
            clone.Scanline = (ScanlineSettings)Scanline.Clone();
            clone.Artistic = (ArtisticSettings)Artistic.Clone();
            clone.PBR = (PbrSettings)PBR.Clone();
            clone.SSR = (SsrSettings)SSR.Clone();
            clone.PCSS = (PcssSettings)PCSS.Clone();
            return clone;
        }
    }
}