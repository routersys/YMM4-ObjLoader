using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Core
{
    public class LayerData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private string _guid = System.Guid.NewGuid().ToString();
        public string Guid { get => _guid; set => Set(ref _guid, value); }

        private string _parentGuid = string.Empty;
        public string ParentGuid { get => _parentGuid; set => Set(ref _parentGuid, value); }

        private string _name = "Layer";
        public string Name { get => _name; set => Set(ref _name, value); }

        private bool _isVisible = true;
        public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

        private string _filePath = string.Empty;
        public string FilePath { get => _filePath; set => Set(ref _filePath, value); }

        private Color _baseColor = Colors.White;
        public Color BaseColor { get => _baseColor; set => Set(ref _baseColor, value); }

        private bool _isLightEnabled = false;
        public bool IsLightEnabled { get => _isLightEnabled; set => Set(ref _isLightEnabled, value); }

        public Animation X { get; set; } = new Animation(0, -100000, 100000);
        public Animation Y { get; set; } = new Animation(0, -100000, 100000);
        public Animation Z { get; set; } = new Animation(0, -100000, 100000);
        public Animation Scale { get; set; } = new Animation(100, 0, 100000);
        public Animation RotationX { get; set; } = new Animation(0, -36000, 36000);
        public Animation RotationY { get; set; } = new Animation(0, -36000, 36000);
        public Animation RotationZ { get; set; } = new Animation(0, -36000, 36000);

        private double _rotationCenterX = 0;
        public double RotationCenterX { get => _rotationCenterX; set => Set(ref _rotationCenterX, value); }

        private double _rotationCenterY = 0;
        public double RotationCenterY { get => _rotationCenterY; set => Set(ref _rotationCenterY, value); }

        private double _rotationCenterZ = 0;
        public double RotationCenterZ { get => _rotationCenterZ; set => Set(ref _rotationCenterZ, value); }

        public Animation Fov { get; set; } = new Animation(45, 1, 179);
        public Animation LightX { get; set; } = new Animation(0, -100000, 100000);
        public Animation LightY { get; set; } = new Animation(0, -100000, 100000);
        public Animation LightZ { get; set; } = new Animation(-100, -100000, 100000);
        public Animation WorldId { get; set; } = new Animation(0, 0, 9);

        private ProjectionType _projection = ProjectionType.Parallel;
        public ProjectionType Projection { get => _projection; set => Set(ref _projection, value); }

        private HashSet<int>? _visibleParts;
        public HashSet<int>? VisibleParts { get => _visibleParts; set => Set(ref _visibleParts, value); }

        private byte[]? _thumbnail;
        public byte[]? Thumbnail { get => _thumbnail; set => Set(ref _thumbnail, value); }

        public LayerData Clone()
        {
            var clone = new LayerData
            {
                Name = Name + " (Copy)",
                IsVisible = IsVisible,
                FilePath = FilePath,
                BaseColor = BaseColor,
                IsLightEnabled = IsLightEnabled,
                Projection = Projection,
                Thumbnail = Thumbnail,
                ParentGuid = ParentGuid,
                RotationCenterX = RotationCenterX,
                RotationCenterY = RotationCenterY,
                RotationCenterZ = RotationCenterZ
            };
            clone.X.CopyFrom(X);
            clone.Y.CopyFrom(Y);
            clone.Z.CopyFrom(Z);
            clone.Scale.CopyFrom(Scale);
            clone.RotationX.CopyFrom(RotationX);
            clone.RotationY.CopyFrom(RotationY);
            clone.RotationZ.CopyFrom(RotationZ);
            clone.Fov.CopyFrom(Fov);
            clone.LightX.CopyFrom(LightX);
            clone.LightY.CopyFrom(LightY);
            clone.LightZ.CopyFrom(LightZ);
            clone.WorldId.CopyFrom(WorldId);

            if (VisibleParts != null)
            {
                clone.VisibleParts = new HashSet<int>(VisibleParts);
            }

            return clone;
        }
    }
}