using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ObjVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
    }
}