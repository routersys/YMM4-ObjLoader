using ObjLoader.Localization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;
using Vector3 = System.Numerics.Vector3;

namespace ObjLoader.ViewModels
{
    public class PartItem : Bindable
    {
        public string Name { get; set; } = "";
        public int Index { get; set; }
        public Vector3 Center { get; set; }
        public double Radius { get; set; }
        public int FaceCount { get; set; }

        public string Detail => string.Format(Texts.SplitWindow_Faces, FaceCount);

        private BitmapSource? _thumbnail;
        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set => Set(ref _thumbnail, value);
        }
    }

    public class MaterialPreset
    {
        public double Roughness { get; set; }
        public double Metallic { get; set; }
        public Color BaseColor { get; set; }
    }
}