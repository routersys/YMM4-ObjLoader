using System.Numerics;
using System.Runtime.CompilerServices;
using ObjLoader.Systems.Models;

namespace ObjLoader.Systems.Physics
{
    public static class PhysicsSolver
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ApplyImpulse(ref PhysicsState state, Vector3 impulse, Vector3 relPos)
        {
            if (state.InverseMass <= 0f) return;
            state.LinearVelocity += impulse * state.InverseMass;
            state.AngularVelocity += Vector3.TransformNormal(Vector3.Cross(relPos, impulse), state.InverseWorldInertia);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyAngularImpulse(ref PhysicsState state, Vector3 impulse)
        {
            if (state.InverseMass <= 0f) return;
            state.AngularVelocity += Vector3.TransformNormal(impulse, state.InverseWorldInertia);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SolveContact(ref PhysicsState stateA, ref PhysicsState stateB, ref ContactPoint contact, float dt, float erpMultiplier)
        {
            bool aStatic = stateA.InverseMass <= 0f;
            bool bStatic = stateB.InverseMass <= 0f;
            if (aStatic && bStatic) return;

            var normal = contact.Normal;
            var relPosA = contact.RelPosA;
            var relPosB = contact.RelPosB;

            float invMassA = aStatic ? 0f : stateA.InverseMass;
            float invMassB = bStatic ? 0f : stateB.InverseMass;
            var invIA = aStatic ? default : stateA.InverseWorldInertia;
            var invIB = bStatic ? default : stateB.InverseWorldInertia;

            if (contact.NormalMass == 0f)
            {
                float denom = PhysicsMath.ComputeImpulseDenominator(
                    invMassA, invIA, relPosA,
                    invMassB, invIB, relPosB, normal);
                contact.NormalMass = denom > 1e-8f ? 1f / denom : 0f;

                if (MathF.Abs(normal.Y) > 0.999f)
                    contact.FrictionDir1 = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitX));
                else
                    contact.FrictionDir1 = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitY));
                contact.FrictionDir2 = Vector3.Cross(normal, contact.FrictionDir1);

                float denomF1 = PhysicsMath.ComputeImpulseDenominator(
                    invMassA, invIA, relPosA,
                    invMassB, invIB, relPosB, contact.FrictionDir1);
                contact.FrictionMass1 = denomF1 > 1e-8f ? 1f / denomF1 : 0f;

                float denomF2 = PhysicsMath.ComputeImpulseDenominator(
                    invMassA, invIA, relPosA,
                    invMassB, invIB, relPosB, contact.FrictionDir2);
                contact.FrictionMass2 = denomF2 > 1e-8f ? 1f / denomF2 : 0f;
            }

            var velA = stateA.LinearVelocity + Vector3.Cross(stateA.AngularVelocity, relPosA);
            var velB = stateB.LinearVelocity + Vector3.Cross(stateB.AngularVelocity, relPosB);
            var relVel = velA - velB;
            float velAlongNormal = Vector3.Dot(relVel, normal);

            float penetration = Math.Min(Math.Max(contact.Depth - 0.005f, 0f), 0.5f);
            float bias = (0.2f * erpMultiplier / dt) * penetration;

            float restitution = MathF.Max(stateA.Restitution, stateB.Restitution);
            if (contact.LifeTime < 1 && velAlongNormal < -1.0f)
                bias += -(restitution * 0.5f) * velAlongNormal;

            float deltaImpulse = -(velAlongNormal - bias) * contact.NormalMass;
            float oldNormalImpulse = contact.AppliedNormalImpulse;
            contact.AppliedNormalImpulse = Math.Max(oldNormalImpulse + deltaImpulse, 0f);
            deltaImpulse = contact.AppliedNormalImpulse - oldNormalImpulse;

            if (MathF.Abs(deltaImpulse) > 1e-8f)
            {
                var imp = normal * deltaImpulse;
                if (!aStatic) ApplyImpulse(ref stateA, imp, relPosA);
                if (!bStatic) ApplyImpulse(ref stateB, -imp, relPosB);
            }

            velA = stateA.LinearVelocity + Vector3.Cross(stateA.AngularVelocity, relPosA);
            velB = stateB.LinearVelocity + Vector3.Cross(stateB.AngularVelocity, relPosB);
            relVel = velA - velB;

            float maxFriction = contact.AppliedNormalImpulse * MathF.Max(stateA.Friction, stateB.Friction);

            float dF1 = -Vector3.Dot(relVel, contact.FrictionDir1) * contact.FrictionMass1;
            float oldF1 = contact.AppliedFrictionImpulse1;
            contact.AppliedFrictionImpulse1 = Math.Clamp(oldF1 + dF1, -maxFriction, maxFriction);
            dF1 = contact.AppliedFrictionImpulse1 - oldF1;

            float dF2 = -Vector3.Dot(relVel, contact.FrictionDir2) * contact.FrictionMass2;
            float oldF2 = contact.AppliedFrictionImpulse2;
            contact.AppliedFrictionImpulse2 = Math.Clamp(oldF2 + dF2, -maxFriction, maxFriction);
            dF2 = contact.AppliedFrictionImpulse2 - oldF2;

            if (MathF.Abs(dF1) > 1e-8f || MathF.Abs(dF2) > 1e-8f)
            {
                var frictionImp = contact.FrictionDir1 * dF1 + contact.FrictionDir2 * dF2;
                if (!aStatic) ApplyImpulse(ref stateA, frictionImp, relPosA);
                if (!bStatic) ApplyImpulse(ref stateB, -frictionImp, relPosB);
            }
        }

        public static void WarmStartContact(ref PhysicsState stateA, ref PhysicsState stateB, ref ContactPoint contact)
        {
            bool aStatic = stateA.InverseMass <= 0f;
            bool bStatic = stateB.InverseMass <= 0f;
            if (aStatic && bStatic) return;

            var normalImpulse = contact.Normal * contact.AppliedNormalImpulse;
            var frictionImpulse = contact.FrictionDir1 * contact.AppliedFrictionImpulse1 + contact.FrictionDir2 * contact.AppliedFrictionImpulse2;
            var totalImpulse = normalImpulse + frictionImpulse;

            if (!aStatic) ApplyImpulse(ref stateA, totalImpulse, contact.RelPosA);
            if (!bStatic) ApplyImpulse(ref stateB, -totalImpulse, contact.RelPosB);
        }

        public static void WarmStartJoint(
            ref PhysicsState stateA, ref PhysicsState stateB,
            Vector3 localAnchorA, Vector3 localAnchorB,
            Quaternion jointFrameA, Quaternion jointFrameB,
            ref Vector3 linearImpulse, ref Vector3 angularImpulse,
            ref Vector3 springLinearImpulse, ref Vector3 springAngularImpulse)
        {
            bool aStatic = stateA.InverseMass <= 0f;
            bool bStatic = stateB.InverseMass <= 0f;
            if (aStatic && bStatic) return;

            var worldAnchorA = stateA.Position + Vector3.Transform(localAnchorA, stateA.Rotation);
            var worldAnchorB = stateB.Position + Vector3.Transform(localAnchorB, stateB.Rotation);
            var relPosA = worldAnchorA - stateA.Position;
            var relPosB = worldAnchorB - stateB.Position;

            var worldFrameA = stateA.Rotation * jointFrameA;
            Vector3 axisX = Vector3.Transform(Vector3.UnitX, worldFrameA);
            Vector3 axisY = Vector3.Transform(Vector3.UnitY, worldFrameA);
            Vector3 axisZ = Vector3.Transform(Vector3.UnitZ, worldFrameA);

            Vector3 worldLinImp = axisX * (linearImpulse.X + springLinearImpulse.X) +
                                  axisY * (linearImpulse.Y + springLinearImpulse.Y) +
                                  axisZ * (linearImpulse.Z + springLinearImpulse.Z);

            Vector3 worldAngImp = axisX * (angularImpulse.X + springAngularImpulse.X) +
                                  axisY * (angularImpulse.Y + springAngularImpulse.Y) +
                                  axisZ * (angularImpulse.Z + springAngularImpulse.Z);

            if (!aStatic)
            {
                ApplyImpulse(ref stateA, worldLinImp, relPosA);
                ApplyAngularImpulse(ref stateA, worldAngImp);
            }
            if (!bStatic)
            {
                ApplyImpulse(ref stateB, -worldLinImp, relPosB);
                ApplyAngularImpulse(ref stateB, -worldAngImp);
            }
        }

        public static void SolveJointConstraint(
            ref PhysicsState stateA, ref PhysicsState stateB, GenericJoint joint,
            Vector3 localAnchorA, Vector3 localAnchorB,
            Quaternion jointFrameA, Quaternion jointFrameB, float dt, float erpMultiplier,
            ref Vector3 linearImpulse, ref Vector3 angularImpulse,
            ref Vector3 springLinearImpulse, ref Vector3 springAngularImpulse)
        {
            bool aStatic = stateA.InverseMass <= 0f;
            bool bStatic = stateB.InverseMass <= 0f;
            if (aStatic && bStatic) return;

            float invMassA = aStatic ? 0f : stateA.InverseMass;
            float invMassB = bStatic ? 0f : stateB.InverseMass;

            var worldAnchorA = stateA.Position + Vector3.Transform(localAnchorA, stateA.Rotation);
            var worldAnchorB = stateB.Position + Vector3.Transform(localAnchorB, stateB.Rotation);
            var relPosA = worldAnchorA - stateA.Position;
            var relPosB = worldAnchorB - stateB.Position;

            SolveLinearLimits(ref stateA, ref stateB, joint, relPosA, relPosB,
                invMassA, invMassB, jointFrameA, aStatic, bStatic, dt, erpMultiplier,
                ref linearImpulse, ref springLinearImpulse);

            SolveAngularLimits(ref stateA, ref stateB, joint,
                jointFrameA, jointFrameB, aStatic, bStatic, dt, erpMultiplier,
                ref angularImpulse, ref springAngularImpulse);
        }

        private static void SolveLinearLimits(
            ref PhysicsState stateA, ref PhysicsState stateB, GenericJoint joint,
            Vector3 relPosA, Vector3 relPosB,
            float invMassA, float invMassB,
            Quaternion jointFrameA,
            bool aStatic, bool bStatic, float dt, float erpMultiplier,
            ref Vector3 linearImpulse, ref Vector3 springLinearImpulse)
        {
            var worldAnchorA = stateA.Position + relPosA;
            var worldAnchorB = stateB.Position + relPosB;
            var diff = worldAnchorB - worldAnchorA;

            var worldFrameA = stateA.Rotation * jointFrameA;
            Vector3 axisX = Vector3.Transform(Vector3.UnitX, worldFrameA);
            Vector3 axisY = Vector3.Transform(Vector3.UnitY, worldFrameA);
            Vector3 axisZ = Vector3.Transform(Vector3.UnitZ, worldFrameA);

            float errX = Vector3.Dot(diff, axisX);
            float errY = Vector3.Dot(diff, axisY);
            float errZ = Vector3.Dot(diff, axisZ);

            SolveAxisLinearLimit(ref stateA, ref stateB, axisX, errX,
                joint.TranslationLimitMin.X, joint.TranslationLimitMax.X, joint.SpringTranslation.X,
                relPosA, relPosB, invMassA, invMassB, aStatic, bStatic, dt, erpMultiplier,
                ref linearImpulse.X, ref springLinearImpulse.X);
            SolveAxisLinearLimit(ref stateA, ref stateB, axisY, errY,
                joint.TranslationLimitMin.Y, joint.TranslationLimitMax.Y, joint.SpringTranslation.Y,
                relPosA, relPosB, invMassA, invMassB, aStatic, bStatic, dt, erpMultiplier,
                ref linearImpulse.Y, ref springLinearImpulse.Y);
            SolveAxisLinearLimit(ref stateA, ref stateB, axisZ, errZ,
                joint.TranslationLimitMin.Z, joint.TranslationLimitMax.Z, joint.SpringTranslation.Z,
                relPosA, relPosB, invMassA, invMassB, aStatic, bStatic, dt, erpMultiplier,
                ref linearImpulse.Z, ref springLinearImpulse.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SolveAxisLinearLimit(
            ref PhysicsState stateA, ref PhysicsState stateB, Vector3 axis, float error,
            float minLim, float maxLim, float ks, Vector3 relPosA, Vector3 relPosB, float invMassA, float invMassB,
            bool aStatic, bool bStatic, float dt, float erpMultiplier,
            ref float accumulatedImpulse, ref float accumulatedSpringImpulse)
        {
            float invM = PhysicsMath.ComputeImpulseDenominator(
                invMassA, stateA.InverseWorldInertia, relPosA,
                invMassB, stateB.InverseWorldInertia, relPosB, axis);

            if (invM <= 1e-8f) return;
            float mass = 1f / invM;

            if (ks > 0f)
            {
                float kd = ks * 0.1f + 0.1f;
                float erp_s = ((dt * ks) / (dt * ks + kd)) * erpMultiplier;
                float cfm_s = 1f / (dt * ks + kd);

                var velA_s = stateA.LinearVelocity + Vector3.Cross(stateA.AngularVelocity, relPosA);
                var velB_s = stateB.LinearVelocity + Vector3.Cross(stateB.AngularVelocity, relPosB);
                float relVelN_s = Vector3.Dot(velA_s - velB_s, axis);

                float target_vel_s = (error * erp_s) / dt;
                float j_s = (target_vel_s - relVelN_s) / (invM + cfm_s);

                float oldSpring = accumulatedSpringImpulse;
                accumulatedSpringImpulse = oldSpring + j_s;
                float deltaSpring = accumulatedSpringImpulse - oldSpring;

                if (MathF.Abs(deltaSpring) > 1e-10f)
                {
                    var imp_s = axis * deltaSpring;
                    if (!aStatic) ApplyImpulse(ref stateA, imp_s, relPosA);
                    if (!bStatic) ApplyImpulse(ref stateB, -imp_s, relPosB);
                }
            }

            if (minLim > maxLim) return;

            float violation = 0f;
            bool isLow = false;
            bool isEquality = (minLim == maxLim);

            if (isEquality)
            {
                violation = error - minLim;
            }
            else
            {
                if (error < minLim)
                {
                    violation = error - minLim;
                    isLow = true;
                }
                else if (error > maxLim)
                {
                    violation = error - maxLim;
                }
                else
                {
                    return;
                }
            }

            var velA_l = stateA.LinearVelocity + Vector3.Cross(stateA.AngularVelocity, relPosA);
            var velB_l = stateB.LinearVelocity + Vector3.Cross(stateB.AngularVelocity, relPosB);
            float relVelN_l = Vector3.Dot(velA_l - velB_l, axis);

            float erpBase = isEquality ? 0.2f : 0.1f;
            float bias = -(erpBase * erpMultiplier / dt) * violation;

            float j = -(relVelN_l + bias) * mass;

            float oldImp = accumulatedImpulse;
            if (!isEquality)
            {
                if (isLow)
                    accumulatedImpulse = MathF.Min(oldImp + j, 0f);
                else
                    accumulatedImpulse = MathF.Max(oldImp + j, 0f);
            }
            else
            {
                accumulatedImpulse = oldImp + j;
            }
            j = accumulatedImpulse - oldImp;

            if (MathF.Abs(j) > 1e-10f)
            {
                var imp = axis * j;
                if (!aStatic) ApplyImpulse(ref stateA, imp, relPosA);
                if (!bStatic) ApplyImpulse(ref stateB, -imp, relPosB);
            }
        }

        private static void SolveAngularLimits(
            ref PhysicsState stateA, ref PhysicsState stateB, GenericJoint joint,
            Quaternion jointFrameA, Quaternion jointFrameB,
            bool aStatic, bool bStatic, float dt, float erpMultiplier,
            ref Vector3 angularImpulse, ref Vector3 springAngularImpulse)
        {
            var worldFrameA = stateA.Rotation * jointFrameA;
            var worldFrameB = stateB.Rotation * jointFrameB;
            var relRot = Quaternion.Inverse(worldFrameA) * worldFrameB;

            var euler = PhysicsMath.QuaternionToEuler(relRot);

            Vector3 axisX = Vector3.Transform(Vector3.UnitX, worldFrameA);
            Vector3 axisY = Vector3.Transform(Vector3.UnitY, worldFrameA);
            Vector3 axisZ = Vector3.Transform(Vector3.UnitZ, worldFrameA);

            SolveAxisAngularLimit(ref stateA, ref stateB, axisX, euler.X,
                joint.RotationLimitMin.X, joint.RotationLimitMax.X, joint.SpringRotation.X,
                aStatic, bStatic, dt, erpMultiplier, ref angularImpulse.X, ref springAngularImpulse.X);
            SolveAxisAngularLimit(ref stateA, ref stateB, axisY, euler.Y,
                joint.RotationLimitMin.Y, joint.RotationLimitMax.Y, joint.SpringRotation.Y,
                aStatic, bStatic, dt, erpMultiplier, ref angularImpulse.Y, ref springAngularImpulse.Y);
            SolveAxisAngularLimit(ref stateA, ref stateB, axisZ, euler.Z,
                joint.RotationLimitMin.Z, joint.RotationLimitMax.Z, joint.SpringRotation.Z,
                aStatic, bStatic, dt, erpMultiplier, ref angularImpulse.Z, ref springAngularImpulse.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SolveAxisAngularLimit(
            ref PhysicsState stateA, ref PhysicsState stateB, Vector3 axis, float error,
            float minLim, float maxLim, float ks, bool aStatic, bool bStatic, float dt, float erpMultiplier,
            ref float accumulatedImpulse, ref float accumulatedSpringImpulse)
        {
            float invM = 0f;
            if (!aStatic) invM += Vector3.Dot(axis, Vector3.TransformNormal(axis, stateA.InverseWorldInertia));
            if (!bStatic) invM += Vector3.Dot(axis, Vector3.TransformNormal(axis, stateB.InverseWorldInertia));

            if (invM <= 1e-8f) return;
            float mass = 1f / invM;

            if (ks > 0f)
            {
                float kd = ks * 0.1f + 0.1f;
                float erp_s = ((dt * ks) / (dt * ks + kd)) * erpMultiplier;
                float cfm_s = 1f / (dt * ks + kd);

                float w_rel_s = 0f;
                if (!aStatic) w_rel_s += Vector3.Dot(stateA.AngularVelocity, axis);
                if (!bStatic) w_rel_s -= Vector3.Dot(stateB.AngularVelocity, axis);

                float target_vel_s = (error * erp_s) / dt;
                float j_s = (target_vel_s - w_rel_s) / (invM + cfm_s);

                float oldSpring = accumulatedSpringImpulse;
                accumulatedSpringImpulse = oldSpring + j_s;
                float deltaSpring = accumulatedSpringImpulse - oldSpring;

                if (MathF.Abs(deltaSpring) > 1e-10f)
                {
                    var imp_s = axis * deltaSpring;
                    if (!aStatic) ApplyAngularImpulse(ref stateA, imp_s);
                    if (!bStatic) ApplyAngularImpulse(ref stateB, -imp_s);
                }
            }

            if (minLim > maxLim) return;

            float violation = 0f;
            bool isLow = false;
            bool isEquality = (minLim == maxLim);

            if (isEquality)
            {
                violation = error - minLim;
            }
            else
            {
                if (error < minLim)
                {
                    violation = error - minLim;
                    isLow = true;
                }
                else if (error > maxLim)
                {
                    violation = error - maxLim;
                }
                else
                {
                    return;
                }
            }

            float relAngVelN = 0f;
            if (!aStatic) relAngVelN += Vector3.Dot(stateA.AngularVelocity, axis);
            if (!bStatic) relAngVelN -= Vector3.Dot(stateB.AngularVelocity, axis);

            float erpBase = isEquality ? 0.2f : 0.1f;
            float bias = -(erpBase * erpMultiplier / dt) * violation;

            float j = -(relAngVelN + bias) * mass;

            float oldImp = accumulatedImpulse;
            if (!isEquality)
            {
                if (isLow)
                    accumulatedImpulse = MathF.Min(oldImp + j, 0f);
                else
                    accumulatedImpulse = MathF.Max(oldImp + j, 0f);
            }
            else
            {
                accumulatedImpulse = oldImp + j;
            }
            j = accumulatedImpulse - oldImp;

            if (MathF.Abs(j) > 1e-10f)
            {
                var imp = axis * j;
                if (!aStatic) ApplyAngularImpulse(ref stateA, imp);
                if (!bStatic) ApplyAngularImpulse(ref stateB, -imp);
            }
        }
    }
}