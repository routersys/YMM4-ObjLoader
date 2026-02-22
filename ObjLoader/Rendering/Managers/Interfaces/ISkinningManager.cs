using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using ObjLoader.Services.Mmd.Animation;
using System;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers.Interfaces
{
    public interface ISkinningManager : IDisposable
    {
        void RegisterSkinningState(string guid, string filePath, ObjVertex[] vertices, VertexBoneWeight[] boneWeights);
        void RemoveSkinningState(string guid);
        void ProcessSkinning(string guid, string filePath, BoneAnimator? animator, double currentTime);
        ID3D11Buffer? GetOverrideVertexBuffer(string guid);
    }
}