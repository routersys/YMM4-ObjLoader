using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Core
{
    public class LayerData
    {
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = "Layer";
        public bool IsVisible { get; set; } = true;

        public string FilePath { get; set; } = string.Empty;
        public Color BaseColor { get; set; } = Colors.White;
        public bool IsLightEnabled { get; set; } = false;

        public Animation X { get; set; } = new Animation(0, -100000, 100000);
        public Animation Y { get; set; } = new Animation(0, -100000, 100000);
        public Animation Z { get; set; } = new Animation(0, -100000, 100000);
        public Animation Scale { get; set; } = new Animation(100, 0, 100000);
        public Animation RotationX { get; set; } = new Animation(0, -36000, 36000);
        public Animation RotationY { get; set; } = new Animation(0, -36000, 36000);
        public Animation RotationZ { get; set; } = new Animation(0, -36000, 36000);
        public Animation Fov { get; set; } = new Animation(45, 1, 179);
        public Animation LightX { get; set; } = new Animation(0, -100000, 100000);
        public Animation LightY { get; set; } = new Animation(0, -100000, 100000);
        public Animation LightZ { get; set; } = new Animation(-100, -100000, 100000);
        public Animation WorldId { get; set; } = new Animation(0, 0, 9);
        public ProjectionType Projection { get; set; } = ProjectionType.Parallel;

        public LayerData Clone()
        {
            var clone = new LayerData
            {
                Name = Name + " (Copy)",
                IsVisible = IsVisible,
                FilePath = FilePath,
                BaseColor = BaseColor,
                IsLightEnabled = IsLightEnabled,
                Projection = Projection
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
            return clone;
        }
    }
}