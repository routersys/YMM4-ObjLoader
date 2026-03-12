using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using Newtonsoft.Json;
using ObjLoader.Plugin;
using ObjLoader.Utilities;
using ObjLoader.Core.Models;
using ObjLoader.Core.Enums;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;

namespace ObjLoader.Core.Timeline
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

        public string Guid { get; set => Set(ref field, value); } = System.Guid.NewGuid().ToString();

        public string ParentGuid { get; set => Set(ref field, value); } = string.Empty;

        public string Name { get; set => Set(ref field, value); } = "Layer";

        public bool IsVisible { get; set => Set(ref field, value); } = true;

        [JsonIgnore]
        private readonly Dictionary<string, string> _layerNameCache = new Dictionary<string, string>();

        [JsonIgnore]
        private string _previousFilePath = string.Empty;

        private string _filePath = string.Empty;
        public string FilePath
        {
            get => _filePath;
            set
            {
                var sanitized = SanitizeFilePath(value);
                if (_filePath == sanitized) return;

                var currentName = Name;
                if (!string.IsNullOrEmpty(currentName) && currentName != "Layer" && currentName != "Default")
                {
                    _layerNameCache[_previousFilePath] = currentName;
                }
                else
                {
                    _layerNameCache.Remove(_previousFilePath);
                }

                _previousFilePath = sanitized;

                if (Set(ref _filePath, sanitized))
                {
                    if (_layerNameCache.TryGetValue(sanitized, out var cachedName))
                    {
                        Name = cachedName;
                    }
                    else
                    {
                        Name = "Default";
                    }
                }
            }
        }

        private string _vmdFilePath = string.Empty;
        public string VmdFilePath
        {
            get => _vmdFilePath;
            set
            {
                if (Set(ref _vmdFilePath, value ?? string.Empty))
                {
                    VmdMotionData = null;
                    BoneAnimatorInstance = null;
                }
            }
        }

        private double _vmdTimeOffset;
        public double VmdTimeOffset { get => _vmdTimeOffset; set => Set(ref _vmdTimeOffset, value); }

        public Color BaseColor { get; set => Set(ref field, value); } = Colors.White;

        public bool IsLightEnabled { get; set => Set(ref field, value); } = false;

        public LightType LightType { get; set => Set(ref field, value); } = LightType.Point;

        public Animation X { get; set; } = new Animation(0, -100000, 100000);
        public Animation Y { get; set; } = new Animation(0, -100000, 100000);
        public Animation Z { get; set; } = new Animation(0, -100000, 100000);
        public Animation Scale { get; set; } = new Animation(100, 0, 100000);
        public Animation RotationX { get; set; } = new Animation(0, -36000, 36000);
        public Animation RotationY { get; set; } = new Animation(0, -36000, 36000);
        public Animation RotationZ { get; set; } = new Animation(0, -36000, 36000);

        public double RotationCenterX { get; set => Set(ref field, value); } = 0;

        public double RotationCenterY { get; set => Set(ref field, value); } = 0;

        public double RotationCenterZ { get; set => Set(ref field, value); } = 0;

        public Animation Fov { get; set; } = new Animation(45, 1, 179);
        public Animation LightX { get; set; } = new Animation(0, -100000, 100000);
        public Animation LightY { get; set; } = new Animation(0, -100000, 100000);
        public Animation LightZ { get; set; } = new Animation(-100, -100000, 100000);
        public Animation WorldId { get; set; } = new Animation(0, 0, 19);

        public ProjectionType Projection { get; set => Set(ref field, value); } = ProjectionType.Parallel;

        public HashSet<int>? VisibleParts { get; set => Set(ref field, value); }

        public byte[]? Thumbnail { get; set => Set(ref field, value); }

        public Dictionary<int, PartMaterialData> PartMaterials { get; set => Set(ref field, value); } = new Dictionary<int, PartMaterialData>();

        [JsonIgnore]
        public VmdData? VmdMotionData { get; set; }

        [JsonIgnore]
        public BoneAnimator? BoneAnimatorInstance { get; set; }

        private static string SanitizeFilePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var trimmed = value.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;

            var result = FileSystemSandbox.Instance.ValidatePath(trimmed);
            if (result.IsAllowed && result.ResolvedPath != null)
            {
                return result.ResolvedPath;
            }

            var basicResult = PathValidator.Validate(trimmed);
            if (basicResult.IsValid && basicResult.NormalizedPath != null)
            {
                return basicResult.NormalizedPath;
            }

            return string.Empty;
        }

        public LayerData Clone()
        {
            var clone = new LayerData
            {
                Name = Name + " (Copy)",
                IsVisible = IsVisible,
                BaseColor = BaseColor,
                IsLightEnabled = IsLightEnabled,
                LightType = LightType,
                Projection = Projection,
                Thumbnail = Thumbnail,
                ParentGuid = ParentGuid,
                RotationCenterX = RotationCenterX,
                RotationCenterY = RotationCenterY,
                RotationCenterZ = RotationCenterZ
            };
            clone._filePath = _filePath;
            clone._previousFilePath = _filePath;
            clone._vmdFilePath = _vmdFilePath;
            clone._vmdTimeOffset = _vmdTimeOffset;
            clone.VmdMotionData = VmdMotionData;
            clone.BoneAnimatorInstance = BoneAnimatorInstance;
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

            if (PartMaterials != null)
            {
                clone.PartMaterials = new Dictionary<int, PartMaterialData>();
                foreach (var kvp in PartMaterials)
                {
                    clone.PartMaterials.Add(kvp.Key, new PartMaterialData
                    {
                        Roughness = kvp.Value.Roughness,
                        Metallic = kvp.Value.Metallic,
                        BaseColor = kvp.Value.BaseColor,
                        TexturePath = kvp.Value.TexturePath
                    });
                }
            }
            return clone;
        }
    }
}