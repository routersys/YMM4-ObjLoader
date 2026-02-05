using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using ObjLoader.Core;

namespace ObjLoader.Plugin.Parameters
{
    internal class ObjLoaderParameterSharedData
    {
        public string FilePath { get; }
        public string ShaderFilePath { get; }
        public Color BaseColor { get; }
        public ProjectionType Projection { get; }
        public Animation ScreenWidth { get; } = new Animation(1920, 1, 8192);
        public Animation ScreenHeight { get; } = new Animation(1080, 1, 8192);
        public Animation X { get; } = new Animation(0, -100000, 100000);
        public Animation Y { get; } = new Animation(0, -100000, 100000);
        public Animation Z { get; } = new Animation(0, -100000, 100000);
        public Animation Scale { get; } = new Animation(100, 0, 100000);
        public Animation RotationX { get; } = new Animation(0, -36000, 36000);
        public Animation RotationY { get; } = new Animation(0, -36000, 36000);
        public Animation RotationZ { get; } = new Animation(0, -36000, 36000);
        public Animation Fov { get; } = new Animation(45, 1, 179);
        public bool IsLightEnabled { get; }
        public Animation LightX { get; } = new Animation(0, -100000, 100000);
        public Animation LightY { get; } = new Animation(0, -100000, 100000);
        public Animation LightZ { get; } = new Animation(-100, -100000, 100000);
        public Animation CameraX { get; } = new Animation(0, -100000, 100000);
        public Animation CameraY { get; } = new Animation(0, -100000, 100000);
        public Animation CameraZ { get; } = new Animation(-2.5, -100000, 100000);
        public Animation TargetX { get; } = new Animation(0, -100000, 100000);
        public Animation TargetY { get; } = new Animation(0, -100000, 100000);
        public Animation TargetZ { get; } = new Animation(0, -100000, 100000);

        public List<LayerData> Layers { get; set; } = new List<LayerData>();
        public int SelectedLayerIndex { get; }

        public ObjLoaderParameterSharedData(ObjLoaderParameter parameter)
        {
            FilePath = parameter.FilePath;
            ShaderFilePath = parameter.ShaderFilePath;
            BaseColor = parameter.BaseColor;
            Projection = parameter.Projection;
            ScreenWidth.CopyFrom(parameter.ScreenWidth);
            ScreenHeight.CopyFrom(parameter.ScreenHeight);
            X.CopyFrom(parameter.X);
            Y.CopyFrom(parameter.Y);
            Z.CopyFrom(parameter.Z);
            Scale.CopyFrom(parameter.Scale);
            RotationX.CopyFrom(parameter.RotationX);
            RotationY.CopyFrom(parameter.RotationY);
            RotationZ.CopyFrom(parameter.RotationZ);
            Fov.CopyFrom(parameter.Fov);
            IsLightEnabled = parameter.IsLightEnabled;
            LightX.CopyFrom(parameter.LightX);
            LightY.CopyFrom(parameter.LightY);
            LightZ.CopyFrom(parameter.LightZ);
            CameraX.CopyFrom(parameter.CameraX);
            CameraY.CopyFrom(parameter.CameraY);
            CameraZ.CopyFrom(parameter.CameraZ);
            TargetX.CopyFrom(parameter.TargetX);
            TargetY.CopyFrom(parameter.TargetY);
            TargetZ.CopyFrom(parameter.TargetZ);

            if (parameter.Layers != null)
                Layers.AddRange(parameter.Layers.Select(l => l.Clone()));
            SelectedLayerIndex = parameter.SelectedLayerIndex;
        }

        public void CopyTo(ObjLoaderParameter parameter)
        {
            parameter.FilePath = FilePath;
            parameter.ShaderFilePath = ShaderFilePath;
            parameter.BaseColor = BaseColor;
            parameter.Projection = Projection;
            parameter.ScreenWidth.CopyFrom(ScreenWidth);
            parameter.ScreenHeight.CopyFrom(ScreenHeight);
            parameter.X.CopyFrom(X);
            parameter.Y.CopyFrom(Y);
            parameter.Z.CopyFrom(Z);
            parameter.Scale.CopyFrom(Scale);
            parameter.RotationX.CopyFrom(RotationX);
            parameter.RotationY.CopyFrom(RotationY);
            parameter.RotationZ.CopyFrom(RotationZ);
            parameter.Fov.CopyFrom(Fov);
            parameter.IsLightEnabled = IsLightEnabled;
            parameter.LightX.CopyFrom(LightX);
            parameter.LightY.CopyFrom(LightY);
            parameter.LightZ.CopyFrom(LightZ);
            parameter.CameraX.CopyFrom(CameraX);
            parameter.CameraY.CopyFrom(CameraY);
            parameter.CameraZ.CopyFrom(CameraZ);
            parameter.TargetX.CopyFrom(TargetX);
            parameter.TargetY.CopyFrom(TargetY);
            parameter.TargetZ.CopyFrom(TargetZ);

            parameter.Layers.Clear();
            if (Layers != null)
            {
                foreach (var layer in Layers)
                {
                    parameter.Layers.Add(layer.Clone());
                }
            }
            parameter.SelectedLayerIndex = SelectedLayerIndex;
        }
    }
}