using System.Numerics;
using ObjLoader.Rendering.Core;
using ObjLoader.Settings;

namespace ObjLoader.Rendering.Utilities
{
    internal static class RenderUtils
    {
        public static Matrix4x4 GetLayerTransform(LayerState state)
        {
            Matrix4x4 axisConversion = Matrix4x4.Identity;
            switch (state.CoordSystem)
            {
                case CoordinateSystem.RightHandedZUp:
                    axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0));
                    break;
                case CoordinateSystem.LeftHandedYUp:
                    axisConversion = Matrix4x4.CreateScale(1, 1, -1);
                    break;
                case CoordinateSystem.LeftHandedZUp:
                    axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)) * Matrix4x4.CreateScale(1, 1, -1);
                    break;
            }

            var rotation = Matrix4x4.CreateRotationX((float)(state.Rx * Math.PI / 180.0)) *
                           Matrix4x4.CreateRotationY((float)(state.Ry * Math.PI / 180.0)) *
                           Matrix4x4.CreateRotationZ((float)(state.Rz * Math.PI / 180.0));
            var scale = Matrix4x4.CreateScale((float)(state.Scale / 100.0));
            var translation = Matrix4x4.CreateTranslation((float)state.X, (float)state.Y, (float)state.Z);

            var center = new Vector3((float)state.Cx, (float)state.Cy, (float)state.Cz);
            var pivotOffset = Matrix4x4.CreateTranslation(-center);

            return pivotOffset * axisConversion * rotation * scale * translation;
        }

        public static System.Numerics.Vector4 ToVec4(System.Windows.Media.Color c) => new System.Numerics.Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
    }
}