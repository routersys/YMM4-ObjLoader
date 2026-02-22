using ObjLoader.Systems.Models;
using System.Numerics;

namespace ObjLoader.Systems.Physics
{
    public static class PhysicsCollision
    {
        public static void DetectCollision(
            int idxA, GenericRigidBody rbA, ref PhysicsState stateA,
            int idxB, GenericRigidBody rbB, ref PhysicsState stateB,
            PersistentManifold manifold)
        {
            if (rbA.ShapeType == 0 && rbB.ShapeType == 0)
                DetectSphereSphere(ref stateA, ref stateB, rbA, rbB, manifold);
            else if (rbA.ShapeType == 0 && rbB.ShapeType == 2)
                DetectSphereCapsule(ref stateA, ref stateB, rbA, rbB, manifold);
            else if (rbA.ShapeType == 2 && rbB.ShapeType == 0)
                DetectCapsuleSphere(ref stateA, ref stateB, rbA, rbB, manifold);
            else if (rbA.ShapeType == 2 && rbB.ShapeType == 2)
                DetectCapsuleCapsule(ref stateA, ref stateB, rbA, rbB, manifold);
            else if (rbA.ShapeType == 1 && rbB.ShapeType == 0)
                DetectBoxSphere(ref stateA, ref stateB, rbA, rbB, manifold);
            else if (rbA.ShapeType == 0 && rbB.ShapeType == 1)
                DetectBoxSphere(ref stateB, ref stateA, rbB, rbA, manifold, true);
            else if (rbA.ShapeType == 1 && rbB.ShapeType == 2)
                DetectBoxCapsule(ref stateA, ref stateB, rbA, rbB, manifold);
            else if (rbA.ShapeType == 2 && rbB.ShapeType == 1)
                DetectBoxCapsule(ref stateB, ref stateA, rbB, rbA, manifold, true);
            else if (rbA.ShapeType == 1 && rbB.ShapeType == 1)
                DetectBoxBox(ref stateA, ref stateB, rbA, rbB, manifold);
            else
                DetectFallbackSphere(ref stateA, ref stateB, rbA, rbB, manifold);
        }

        private static float GetRadius(GenericRigidBody rb)
        {
            if (rb.ShapeType == 0) return rb.ShapeSize.X;
            if (rb.ShapeType == 1) return MathF.Max(rb.ShapeSize.X, MathF.Max(rb.ShapeSize.Y, rb.ShapeSize.Z));
            if (rb.ShapeType == 2) return rb.ShapeSize.X;
            return 0.1f;
        }

        private static void DetectFallbackSphere(
            ref PhysicsState stateA, ref PhysicsState stateB,
            GenericRigidBody rbA, GenericRigidBody rbB, PersistentManifold manifold)
        {
            float rA = GetRadius(rbA);
            float rB = GetRadius(rbB);
            var delta = stateA.Position - stateB.Position;
            float distSq = delta.LengthSquared();
            float rSum = rA + rB;
            if (distSq < rSum * rSum && distSq > 1e-8f)
            {
                float dist = MathF.Sqrt(distSq);
                var normal = delta / dist;
                float depth = rSum - dist;
                var pA = -normal * rA;
                var pB = normal * rB;
                var worldA = stateA.Position + pA;
                var worldB = stateB.Position + pB;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateA.Position,
                    RelPosB = worldContact - stateB.Position,
                    LocalPointA = Vector3.Transform(pA, Quaternion.Inverse(stateA.Rotation)),
                    LocalPointB = Vector3.Transform(pB, Quaternion.Inverse(stateB.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
            else if (distSq <= 1e-8f)
            {
                var normal = Vector3.UnitY;
                float depth = rSum;
                var pA = -normal * rA;
                var pB = normal * rB;
                var worldA = stateA.Position + pA;
                var worldB = stateB.Position + pB;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateA.Position,
                    RelPosB = worldContact - stateB.Position,
                    LocalPointA = Vector3.Transform(pA, Quaternion.Inverse(stateA.Rotation)),
                    LocalPointB = Vector3.Transform(pB, Quaternion.Inverse(stateB.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
        }

        private static void DetectSphereSphere(
            ref PhysicsState stateA, ref PhysicsState stateB,
            GenericRigidBody rbA, GenericRigidBody rbB, PersistentManifold manifold)
        {
            float rA = rbA.ShapeSize.X;
            float rB = rbB.ShapeSize.X;
            var delta = stateA.Position - stateB.Position;
            float distSq = delta.LengthSquared();
            float rSum = rA + rB;

            if (distSq < rSum * rSum && distSq > 1e-8f)
            {
                float dist = MathF.Sqrt(distSq);
                var normal = delta / dist;
                float depth = rSum - dist;
                var pA = -normal * rA;
                var pB = normal * rB;
                var worldA = stateA.Position + pA;
                var worldB = stateB.Position + pB;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateA.Position,
                    RelPosB = worldContact - stateB.Position,
                    LocalPointA = Vector3.Transform(pA, Quaternion.Inverse(stateA.Rotation)),
                    LocalPointB = Vector3.Transform(pB, Quaternion.Inverse(stateB.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
            else if (distSq <= 1e-8f)
            {
                var normal = Vector3.UnitY;
                float depth = rSum;
                var pA = -normal * rA;
                var pB = normal * rB;
                var worldA = stateA.Position + pA;
                var worldB = stateB.Position + pB;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateA.Position,
                    RelPosB = worldContact - stateB.Position,
                    LocalPointA = Vector3.Transform(pA, Quaternion.Inverse(stateA.Rotation)),
                    LocalPointB = Vector3.Transform(pB, Quaternion.Inverse(stateB.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
        }

        private static void DetectSphereCapsule(
            ref PhysicsState stateS, ref PhysicsState stateC,
            GenericRigidBody sphere, GenericRigidBody capsule, PersistentManifold manifold)
        {
            float rS = sphere.ShapeSize.X;
            float rC = capsule.ShapeSize.X;
            float hC = capsule.ShapeSize.Y;

            var capAxis = Vector3.TransformNormal(Vector3.UnitY, Matrix4x4.CreateFromQuaternion(stateC.Rotation));
            var capH = capAxis * (hC * 0.5f);
            var p1 = stateC.Position - capH;
            var p2 = stateC.Position + capH;

            var closestOnCap = PhysicsMath.ClosestPointOnSegment(stateS.Position, p1, p2, out _);
            var delta = stateS.Position - closestOnCap;
            float distSq = delta.LengthSquared();
            float rSum = rS + rC;

            if (distSq < rSum * rSum && distSq > 1e-8f)
            {
                float dist = MathF.Sqrt(distSq);
                var normal = delta / dist;
                float depth = rSum - dist;
                var pA = -normal * rS;
                var pB = (closestOnCap + normal * rC) - stateC.Position;
                var worldA = stateS.Position + pA;
                var worldB = stateC.Position + pB;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateS.Position,
                    RelPosB = worldContact - stateC.Position,
                    LocalPointA = Vector3.Transform(pA, Quaternion.Inverse(stateS.Rotation)),
                    LocalPointB = Vector3.Transform(pB, Quaternion.Inverse(stateC.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
            else if (distSq <= 1e-8f)
            {
                var normal = Vector3.UnitY;
                float depth = rSum;
                var pA = -normal * rS;
                var pB = (closestOnCap + normal * rC) - stateC.Position;
                var worldA = stateS.Position + pA;
                var worldB = stateC.Position + pB;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateS.Position,
                    RelPosB = worldContact - stateC.Position,
                    LocalPointA = Vector3.Transform(pA, Quaternion.Inverse(stateS.Rotation)),
                    LocalPointB = Vector3.Transform(pB, Quaternion.Inverse(stateC.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
        }

        private static void DetectCapsuleSphere(
            ref PhysicsState stateC, ref PhysicsState stateS,
            GenericRigidBody capsule, GenericRigidBody sphere, PersistentManifold manifold)
        {
            float rC = capsule.ShapeSize.X;
            float hC = capsule.ShapeSize.Y;
            float rS = sphere.ShapeSize.X;

            var capAxis = Vector3.TransformNormal(Vector3.UnitY, Matrix4x4.CreateFromQuaternion(stateC.Rotation));
            var capH = capAxis * (hC * 0.5f);
            var p1 = stateC.Position - capH;
            var p2 = stateC.Position + capH;

            var closestOnCap = PhysicsMath.ClosestPointOnSegment(stateS.Position, p1, p2, out _);
            var deltaClosest = closestOnCap - stateS.Position;
            float distSq = deltaClosest.LengthSquared();
            float rSum = rC + rS;

            if (distSq < rSum * rSum && distSq > 1e-8f)
            {
                float dist = MathF.Sqrt(distSq);
                var normal = -deltaClosest / dist;
                float depth = rSum - dist;
                var pA = (closestOnCap - normal * rC) - stateC.Position;
                var pB = normal * rS;
                var worldA = stateC.Position + pA;
                var worldB = stateS.Position + pB;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateC.Position,
                    RelPosB = worldContact - stateS.Position,
                    LocalPointA = Vector3.Transform(pA, Quaternion.Inverse(stateC.Rotation)),
                    LocalPointB = Vector3.Transform(pB, Quaternion.Inverse(stateS.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
            else if (distSq <= 1e-8f)
            {
                var normal = Vector3.UnitY;
                float depth = rSum;
                var pA = (closestOnCap - normal * rC) - stateC.Position;
                var pB = normal * rS;
                var worldA = stateC.Position + pA;
                var worldB = stateS.Position + pB;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateC.Position,
                    RelPosB = worldContact - stateS.Position,
                    LocalPointA = Vector3.Transform(pA, Quaternion.Inverse(stateC.Rotation)),
                    LocalPointB = Vector3.Transform(pB, Quaternion.Inverse(stateS.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
        }

        private static void DetectCapsuleCapsule(
            ref PhysicsState stateA, ref PhysicsState stateB,
            GenericRigidBody capA, GenericRigidBody capB, PersistentManifold manifold)
        {
            float rA = capA.ShapeSize.X;
            float hA = capA.ShapeSize.Y;
            float rB = capB.ShapeSize.X;
            float hB = capB.ShapeSize.Y;

            var axisA = Vector3.TransformNormal(Vector3.UnitY, Matrix4x4.CreateFromQuaternion(stateA.Rotation));
            var pA1 = stateA.Position - axisA * (hA * 0.5f);
            var pA2 = stateA.Position + axisA * (hA * 0.5f);

            var axisB = Vector3.TransformNormal(Vector3.UnitY, Matrix4x4.CreateFromQuaternion(stateB.Rotation));
            var pB1 = stateB.Position - axisB * (hB * 0.5f);
            var pB2 = stateB.Position + axisB * (hB * 0.5f);

            PhysicsMath.ClosestPointsSegmentSegment(pA1, pA2, pB1, pB2, out var closestA, out var closestB);

            var delta = closestA - closestB;
            float distSq = delta.LengthSquared();
            float rSum = rA + rB;
            float marginSq = (rSum + 0.015f) * (rSum + 0.015f);

            if (distSq < marginSq && distSq > 1e-8f)
            {
                float dist = MathF.Sqrt(distSq);
                var normal = delta / dist;
                float depth = rSum - dist;
                var pALocal = (closestA - normal * rA) - stateA.Position;
                var pBLocal = (closestB + normal * rB) - stateB.Position;
                var worldA = stateA.Position + pALocal;
                var worldB = stateB.Position + pBLocal;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateA.Position,
                    RelPosB = worldContact - stateB.Position,
                    LocalPointA = Vector3.Transform(pALocal, Quaternion.Inverse(stateA.Rotation)),
                    LocalPointB = Vector3.Transform(pBLocal, Quaternion.Inverse(stateB.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
            else if (distSq <= 1e-8f)
            {
                var normal = Vector3.UnitY;
                float depth = rSum;
                var pALocal = (closestA - normal * rA) - stateA.Position;
                var pBLocal = (closestB + normal * rB) - stateB.Position;
                var worldA = stateA.Position + pALocal;
                var worldB = stateB.Position + pBLocal;
                var worldContact = (worldA + worldB) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = normal,
                    Depth = depth,
                    RelPosA = worldContact - stateA.Position,
                    RelPosB = worldContact - stateB.Position,
                    LocalPointA = Vector3.Transform(pALocal, Quaternion.Inverse(stateA.Rotation)),
                    LocalPointB = Vector3.Transform(pBLocal, Quaternion.Inverse(stateB.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
        }

        private static void DetectBoxSphere(
            ref PhysicsState stateBox, ref PhysicsState stateSphere,
            GenericRigidBody box, GenericRigidBody sphere, PersistentManifold manifold, bool flip = false)
        {
            float rS = sphere.ShapeSize.X;
            var extents = box.ShapeSize;

            var localSpherePos = Vector3.Transform(stateSphere.Position - stateBox.Position, Quaternion.Inverse(stateBox.Rotation));

            var closestOnBox = new Vector3(
                Math.Clamp(localSpherePos.X, -extents.X, extents.X),
                Math.Clamp(localSpherePos.Y, -extents.Y, extents.Y),
                Math.Clamp(localSpherePos.Z, -extents.Z, extents.Z));

            var deltaLocal = localSpherePos - closestOnBox;
            float distSq = deltaLocal.LengthSquared();

            if (distSq < rS * rS && distSq > 1e-8f)
            {
                float dist = MathF.Sqrt(distSq);
                var localNormal = deltaLocal / dist;
                float depth = rS - dist;

                var worldNormal = Vector3.TransformNormal(localNormal, Matrix4x4.CreateFromQuaternion(stateBox.Rotation));
                var worldClosestOnBox = stateBox.Position + Vector3.Transform(closestOnBox, stateBox.Rotation);

                var pBox = worldClosestOnBox - stateBox.Position;
                var pSphere = -worldNormal * rS;
                var worldBoxPoint = stateBox.Position + pBox;
                var worldSpherePoint = stateSphere.Position + pSphere;
                var worldContact = (worldBoxPoint + worldSpherePoint) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = flip ? worldNormal : -worldNormal,
                    Depth = depth,
                    RelPosA = flip ? (worldContact - stateSphere.Position) : (worldContact - stateBox.Position),
                    RelPosB = flip ? (worldContact - stateBox.Position) : (worldContact - stateSphere.Position),
                    LocalPointA = Vector3.Transform(flip ? pSphere : pBox, Quaternion.Inverse(flip ? stateSphere.Rotation : stateBox.Rotation)),
                    LocalPointB = Vector3.Transform(flip ? pBox : pSphere, Quaternion.Inverse(flip ? stateBox.Rotation : stateSphere.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
            else if (distSq <= 1e-8f)
            {
                float dx = extents.X - MathF.Abs(localSpherePos.X);
                float dy = extents.Y - MathF.Abs(localSpherePos.Y);
                float dz = extents.Z - MathF.Abs(localSpherePos.Z);

                var localNormal = Vector3.Zero;
                float depth = rS;

                if (dx < dy && dx < dz)
                {
                    localNormal.X = localSpherePos.X > 0 ? 1 : -1;
                    depth += dx;
                }
                else if (dy < dx && dy < dz)
                {
                    localNormal.Y = localSpherePos.Y > 0 ? 1 : -1;
                    depth += dy;
                }
                else
                {
                    localNormal.Z = localSpherePos.Z > 0 ? 1 : -1;
                    depth += dz;
                }

                var worldNormal = Vector3.TransformNormal(localNormal, Matrix4x4.CreateFromQuaternion(stateBox.Rotation));
                var worldClosestOnBox = stateSphere.Position;

                var pBox = worldClosestOnBox - stateBox.Position;
                var pSphere = -worldNormal * rS;
                var worldBoxPoint = stateBox.Position + pBox;
                var worldSpherePoint = stateSphere.Position + pSphere;
                var worldContact = (worldBoxPoint + worldSpherePoint) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = flip ? worldNormal : -worldNormal,
                    Depth = depth,
                    RelPosA = flip ? (worldContact - stateSphere.Position) : (worldContact - stateBox.Position),
                    RelPosB = flip ? (worldContact - stateBox.Position) : (worldContact - stateSphere.Position),
                    LocalPointA = Vector3.Transform(flip ? pSphere : pBox, Quaternion.Inverse(flip ? stateSphere.Rotation : stateBox.Rotation)),
                    LocalPointB = Vector3.Transform(flip ? pBox : pSphere, Quaternion.Inverse(flip ? stateBox.Rotation : stateSphere.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
        }

        private static void DetectBoxCapsule(
            ref PhysicsState stateBox, ref PhysicsState stateCap,
            GenericRigidBody box, GenericRigidBody cap, PersistentManifold manifold, bool flip = false)
        {
            float rC = cap.ShapeSize.X;
            float hC = cap.ShapeSize.Y;
            var extents = box.ShapeSize;

            var capAxis = Vector3.TransformNormal(Vector3.UnitY, Matrix4x4.CreateFromQuaternion(stateCap.Rotation));
            var capH = capAxis * (hC * 0.5f);
            var p1 = stateCap.Position - capH;
            var p2 = stateCap.Position + capH;

            var localP1 = Vector3.Transform(p1 - stateBox.Position, Quaternion.Inverse(stateBox.Rotation));
            var localP2 = Vector3.Transform(p2 - stateBox.Position, Quaternion.Inverse(stateBox.Rotation));

            var localClosestOnCap = PhysicsMath.ClosestPointOnSegment(Vector3.Zero, localP1, localP2, out _);

            var clampedClosestOnBox = new Vector3(
                Math.Clamp(localClosestOnCap.X, -extents.X, extents.X),
                Math.Clamp(localClosestOnCap.Y, -extents.Y, extents.Y),
                Math.Clamp(localClosestOnCap.Z, -extents.Z, extents.Z));

            var newLocalClosestOnCap = PhysicsMath.ClosestPointOnSegment(clampedClosestOnBox, localP1, localP2, out _);

            var finalClampedOnBox = new Vector3(
                Math.Clamp(newLocalClosestOnCap.X, -extents.X, extents.X),
                Math.Clamp(newLocalClosestOnCap.Y, -extents.Y, extents.Y),
                Math.Clamp(newLocalClosestOnCap.Z, -extents.Z, extents.Z));

            var deltaLocal = newLocalClosestOnCap - finalClampedOnBox;
            float distSq = deltaLocal.LengthSquared();

            if (distSq < rC * rC && distSq > 1e-8f)
            {
                float dist = MathF.Sqrt(distSq);
                var localNormal = deltaLocal / dist;
                float depth = rC - dist;

                var worldNormal = Vector3.TransformNormal(localNormal, Matrix4x4.CreateFromQuaternion(stateBox.Rotation));
                var worldClosestOnBox = stateBox.Position + Vector3.Transform(finalClampedOnBox, stateBox.Rotation);
                var worldClosestOnCap = stateBox.Position + Vector3.Transform(newLocalClosestOnCap, stateBox.Rotation);

                var pBox = worldClosestOnBox - stateBox.Position;
                var pCap = (worldClosestOnCap - worldNormal * rC) - stateCap.Position;
                var worldBoxPoint = stateBox.Position + pBox;
                var worldCapPoint = stateCap.Position + pCap;
                var worldContact = (worldBoxPoint + worldCapPoint) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = flip ? worldNormal : -worldNormal,
                    Depth = depth,
                    RelPosA = flip ? (worldContact - stateCap.Position) : (worldContact - stateBox.Position),
                    RelPosB = flip ? (worldContact - stateBox.Position) : (worldContact - stateCap.Position),
                    LocalPointA = Vector3.Transform(flip ? pCap : pBox, Quaternion.Inverse(flip ? stateCap.Rotation : stateBox.Rotation)),
                    LocalPointB = Vector3.Transform(flip ? pBox : pCap, Quaternion.Inverse(flip ? stateBox.Rotation : stateCap.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
            else if (distSq <= 1e-8f)
            {
                float dx = extents.X - MathF.Abs(newLocalClosestOnCap.X);
                float dy = extents.Y - MathF.Abs(newLocalClosestOnCap.Y);
                float dz = extents.Z - MathF.Abs(newLocalClosestOnCap.Z);

                var localNormal = Vector3.Zero;
                float depth = rC;

                if (dx < dy && dx < dz)
                {
                    localNormal.X = newLocalClosestOnCap.X > 0 ? 1 : -1;
                    depth += dx;
                }
                else if (dy < dx && dy < dz)
                {
                    localNormal.Y = newLocalClosestOnCap.Y > 0 ? 1 : -1;
                    depth += dy;
                }
                else
                {
                    localNormal.Z = newLocalClosestOnCap.Z > 0 ? 1 : -1;
                    depth += dz;
                }

                var worldNormal = Vector3.TransformNormal(localNormal, Matrix4x4.CreateFromQuaternion(stateBox.Rotation));
                var worldClosestOnCap = stateBox.Position + Vector3.Transform(newLocalClosestOnCap, stateBox.Rotation);
                var worldClosestOnBox = worldClosestOnCap;

                var pBox = worldClosestOnBox - stateBox.Position;
                var pCap = (worldClosestOnCap - worldNormal * rC) - stateCap.Position;
                var worldBoxPoint = stateBox.Position + pBox;
                var worldCapPoint = stateCap.Position + pCap;
                var worldContact = (worldBoxPoint + worldCapPoint) * 0.5f;

                var pt = new ContactPoint
                {
                    Normal = flip ? worldNormal : -worldNormal,
                    Depth = depth,
                    RelPosA = flip ? (worldContact - stateCap.Position) : (worldContact - stateBox.Position),
                    RelPosB = flip ? (worldContact - stateBox.Position) : (worldContact - stateCap.Position),
                    LocalPointA = Vector3.Transform(flip ? pCap : pBox, Quaternion.Inverse(flip ? stateCap.Rotation : stateBox.Rotation)),
                    LocalPointB = Vector3.Transform(flip ? pBox : pCap, Quaternion.Inverse(flip ? stateBox.Rotation : stateCap.Rotation))
                };
                manifold.AddContactPoint(ref pt);
            }
        }

        private static Vector3 GetBoxSupportVertex(ref PhysicsState state, Vector3 extents, ReadOnlySpan<Vector3> axes, Vector3 direction)
        {
            var p = state.Position;
            p += axes[0] * MathF.CopySign(extents.X, Vector3.Dot(axes[0], direction));
            p += axes[1] * MathF.CopySign(extents.Y, Vector3.Dot(axes[1], direction));
            p += axes[2] * MathF.CopySign(extents.Z, Vector3.Dot(axes[2], direction));
            return p;
        }

        private static void DetectBoxBox(
            ref PhysicsState stateA, ref PhysicsState stateB,
            GenericRigidBody boxA, GenericRigidBody boxB, PersistentManifold manifold)
        {
            var extA = boxA.ShapeSize;
            var extB = boxB.ShapeSize;

            var rotA = Matrix4x4.CreateFromQuaternion(stateA.Rotation);
            var rotB = Matrix4x4.CreateFromQuaternion(stateB.Rotation);

            Span<Vector3> axesA = stackalloc Vector3[3];
            axesA[0] = new Vector3(rotA.M11, rotA.M12, rotA.M13);
            axesA[1] = new Vector3(rotA.M21, rotA.M22, rotA.M23);
            axesA[2] = new Vector3(rotA.M31, rotA.M32, rotA.M33);

            Span<Vector3> axesB = stackalloc Vector3[3];
            axesB[0] = new Vector3(rotB.M11, rotB.M12, rotB.M13);
            axesB[1] = new Vector3(rotB.M21, rotB.M22, rotB.M23);
            axesB[2] = new Vector3(rotB.M31, rotB.M32, rotB.M33);

            var delta = stateB.Position - stateA.Position;

            float minPenetration = float.MaxValue;
            Vector3 bestAxis = Vector3.Zero;
            int bestAxisIndex = -1;

            Span<Vector3> axes = stackalloc Vector3[15];
            axes[0] = axesA[0]; axes[1] = axesA[1]; axes[2] = axesA[2];
            axes[3] = axesB[0]; axes[4] = axesB[1]; axes[5] = axesB[2];
            int idx = 6;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var c = Vector3.Cross(axesA[i], axesB[j]);
                    if (c.LengthSquared() > 1e-6f)
                        axes[idx] = Vector3.Normalize(c);
                    else
                        axes[idx] = Vector3.Zero;
                    idx++;
                }
            }

            for (int i = 0; i < 15; i++)
            {
                var axis = axes[i];
                if (axis == Vector3.Zero) continue;

                float projA = extA.X * MathF.Abs(Vector3.Dot(axis, axesA[0])) +
                              extA.Y * MathF.Abs(Vector3.Dot(axis, axesA[1])) +
                              extA.Z * MathF.Abs(Vector3.Dot(axis, axesA[2]));

                float projB = extB.X * MathF.Abs(Vector3.Dot(axis, axesB[0])) +
                              extB.Y * MathF.Abs(Vector3.Dot(axis, axesB[1])) +
                              extB.Z * MathF.Abs(Vector3.Dot(axis, axesB[2]));

                float dist = MathF.Abs(Vector3.Dot(delta, axis));
                float penetration = projA + projB - dist;

                if (penetration < 0f) return;

                if (penetration < minPenetration)
                {
                    minPenetration = penetration;
                    bestAxis = Vector3.Dot(delta, axis) < 0 ? -axis : axis;
                    bestAxisIndex = i;
                }
            }

            Vector3 pA, pB;
            if (bestAxisIndex < 3)
            {
                pB = GetBoxSupportVertex(ref stateB, extB, axesB, -bestAxis);
                pA = pB - bestAxis * minPenetration;
            }
            else if (bestAxisIndex < 6)
            {
                pA = GetBoxSupportVertex(ref stateA, extA, axesA, bestAxis);
                pB = pA + bestAxis * minPenetration;
            }
            else
            {
                pA = GetBoxSupportVertex(ref stateA, extA, axesA, bestAxis);
                pB = GetBoxSupportVertex(ref stateB, extB, axesB, -bestAxis);
                var mid = (pA + pB) * 0.5f;
                pA = mid - bestAxis * (minPenetration * 0.5f);
                pB = mid + bestAxis * (minPenetration * 0.5f);
            }

            var worldContact = (pA + pB) * 0.5f;

            var pt = new ContactPoint
            {
                Normal = -bestAxis,
                Depth = minPenetration,
                RelPosA = worldContact - stateA.Position,
                RelPosB = worldContact - stateB.Position,
                LocalPointA = Vector3.Transform(pA - stateA.Position, Quaternion.Inverse(stateA.Rotation)),
                LocalPointB = Vector3.Transform(pB - stateB.Position, Quaternion.Inverse(stateB.Rotation))
            };
            manifold.AddContactPoint(ref pt);
        }
    }
}
