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
        public Vector4 ToonParams;
        public Vector4 RimParams;
        public Vector4 RimColor;
        public Vector4 OutlineParams;
        public Vector4 OutlineColor;
        public Vector4 FogParams;
        public Vector4 FogColor;
        public Vector4 ColorCorrParams;
        public Vector4 VignetteParams;
        public Vector4 VignetteColor;
        public Vector4 ScanlineParams;
        public Vector4 ChromAbParams;
        public Vector4 MonoParams;
        public Vector4 MonoColor;
        public Vector4 PosterizeParams;
        public Vector4 LightTypeParams;
        public Matrix4x4 LightViewProj0;
        public Matrix4x4 LightViewProj1;
        public Matrix4x4 LightViewProj2;
        public Vector4 ShadowParams;
        public Vector4 CascadeSplits;
    }
}