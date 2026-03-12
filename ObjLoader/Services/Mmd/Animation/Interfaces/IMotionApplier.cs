using System.Numerics;
using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.Services.Mmd.Parsers;

namespace ObjLoader.Services.Mmd.Animation.Interfaces;

public interface IMotionApplier
{
    List<CameraKeyframe> ConvertCameraFrames(VmdData vmdData, Vector3 modelCenter, float modelScale);
    double GetDuration(VmdData vmdData);
    ObjVertex[] ApplySkinning(ObjVertex[] original, VertexBoneWeight[] weights, Matrix4x4[] boneTransforms);
}