using System.Numerics;

namespace ObjLoader.Services.Mmd.Animation.Interfaces
{
    public interface IAnimator
    {
        Matrix4x4[] ComputeBoneTransforms(double timeSeconds);
    }
}