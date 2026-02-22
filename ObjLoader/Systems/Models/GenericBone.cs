using System.Numerics;

namespace ObjLoader.Systems.Models
{
    public class GenericBone
    {
        public string Name { get; set; } = string.Empty;
        public int ParentIndex { get; set; } = -1;
        public Vector3 Position { get; set; }
    }
}