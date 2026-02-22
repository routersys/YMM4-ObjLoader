using System.Numerics;

namespace ObjLoader.Systems.Physics
{
    public class PersistentManifold
    {
        public int BodyA;
        public int BodyB;
        public ContactPoint[] Points = new ContactPoint[4];
        public int PointCount = 0;

        public PersistentManifold(int bodyA, int bodyB)
        {
            BodyA = bodyA;
            BodyB = bodyB;
        }

        public void AddContactPoint(ref ContactPoint newPoint)
        {
            int insertIdx = PointCount;
            if (insertIdx == 4)
            {
                int minDepthIdx = -1;
                float minDepth = newPoint.Depth;
                for (int i = 0; i < 4; i++)
                {
                    if (Points[i].Depth < minDepth)
                    {
                        minDepth = Points[i].Depth;
                        minDepthIdx = i;
                    }
                }
                if (minDepthIdx >= 0) insertIdx = minDepthIdx;
                else return;
            }

            int match = -1;
            const float ThresholdSq = 0.04f * 0.04f;
            for (int i = 0; i < PointCount; i++)
            {
                if (Vector3.DistanceSquared(Points[i].LocalPointA, newPoint.LocalPointA) < ThresholdSq &&
                    Vector3.DistanceSquared(Points[i].LocalPointB, newPoint.LocalPointB) < ThresholdSq)
                {
                    match = i;
                    break;
                }
            }

            if (match >= 0)
            {
                newPoint.AppliedNormalImpulse = Points[match].AppliedNormalImpulse;
                newPoint.AppliedFrictionImpulse1 = Points[match].AppliedFrictionImpulse1;
                newPoint.AppliedFrictionImpulse2 = Points[match].AppliedFrictionImpulse2;
                newPoint.FrictionDir1 = Points[match].FrictionDir1;
                newPoint.FrictionDir2 = Points[match].FrictionDir2;
                newPoint.LifeTime = Points[match].LifeTime + 1;
                Points[match] = newPoint;
                return;
            }

            newPoint.AppliedNormalImpulse = 0f;
            newPoint.AppliedFrictionImpulse1 = 0f;
            newPoint.AppliedFrictionImpulse2 = 0f;
            newPoint.LifeTime = 0;

            Points[insertIdx] = newPoint;
            if (insertIdx == PointCount) PointCount++;
        }

        public void RefreshContactPoints(ref PhysicsState stateA, ref PhysicsState stateB)
        {
            const float BreakThresholdSq = 0.1f * 0.1f;
            for (int i = PointCount - 1; i >= 0; i--)
            {
                var pA = stateA.Position + Vector3.Transform(Points[i].LocalPointA, stateA.Rotation);
                var pB = stateB.Position + Vector3.Transform(Points[i].LocalPointB, stateB.Rotation);
                var normal = Points[i].Normal;
                var dist = Vector3.Dot(pA - pB, normal);
                var projA = pA - normal * dist;

                if (dist > 0.1f || Vector3.DistanceSquared(projA, pB) > BreakThresholdSq)
                {
                    Points[i] = Points[PointCount - 1];
                    PointCount--;
                }
                else
                {
                    Points[i].Depth = -dist;
                    var worldContact = (pA + pB) * 0.5f;
                    Points[i].RelPosA = worldContact - stateA.Position;
                    Points[i].RelPosB = worldContact - stateB.Position;
                    Points[i].NormalMass = 0f;
                    Points[i].FrictionMass1 = 0f;
                    Points[i].FrictionMass2 = 0f;
                }
            }
        }
    }
}