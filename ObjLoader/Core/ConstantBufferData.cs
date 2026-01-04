using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Core
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ConstantBufferData
    {
        public Matrix4x4 WorldViewProj;
        public Matrix4x4 World;
        public Vector4 LightPos;
        public Vector4 BaseColor;
        public Vector4 AmbientColor;
        public Vector4 LightColor;
        public Vector4 CameraPos;
        public float LightEnabled;
        public float DiffuseIntensity;
        public float SpecularIntensity;
        public float Shininess;
        public Vector4 GridColor;
        public Vector4 GridAxisColor;
    }
}