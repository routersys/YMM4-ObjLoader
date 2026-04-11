using ObjLoader.Api.Core;
using System.Numerics;

namespace ObjLoader.Api.Raycast
{
    public sealed class RaycastHit
    {
        public SceneObjectId ObjectId { get; }
        public int PartIndex { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Normal { get; }
        public float Distance { get; }

        public RaycastHit(SceneObjectId objectId, int partIndex, Vector3 hitPoint, Vector3 normal, float distance)
        {
            ObjectId = objectId;
            PartIndex = partIndex;
            HitPoint = hitPoint;
            Normal = normal;
            Distance = distance;
        }
    }
}