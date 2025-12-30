using System;
using System.Collections.Generic;
using System.Numerics;

namespace ObjLoader.Core
{
    public class ObjModel
    {
        public ObjVertex[] Vertices { get; set; } = Array.Empty<ObjVertex>();
        public int[] Indices { get; set; } = Array.Empty<int>();
        public List<ModelPart> Parts { get; set; } = new List<ModelPart>();
        public Vector3 ModelCenter { get; set; }
        public float ModelScale { get; set; } = 1.0f;
    }
}