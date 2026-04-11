using ObjLoader.Settings;
using ObjLoader.Systems.Models;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ObjLoader.Systems.Physics;

public class GenericPhysicsEngine : IDisposable
{
    private float Gravity => (float)PluginSettings.Instance.PhysicsGravity;
    private const float FixedTimeStep = 1f / 60f;
    private int MaxSubSteps => PluginSettings.Instance.PhysicsMaxSubSteps;
    private int SolverIterations => PluginSettings.Instance.PhysicsSolverIterations;
    private bool EnableGroundCollision => PluginSettings.Instance.PhysicsGroundCollision;
    private float GroundY => (float)PluginSettings.Instance.PhysicsGroundY;
    private float SleepLinearThreshold => (float)PluginSettings.Instance.PhysicsSleepLinearThreshold;
    private float SleepAngularThreshold => (float)PluginSettings.Instance.PhysicsSleepAngularThreshold;
    private float SleepTimeRequired => (float)PluginSettings.Instance.PhysicsSleepTimeRequired;
    private int MaxManifolds => PluginSettings.Instance.PhysicsMaxManifolds;
    private int ParallelNarrowPhaseThreshold => PluginSettings.Instance.PhysicsParallelNarrowPhaseThreshold;
    private float WarmStartScale => (float)PluginSettings.Instance.PhysicsWarmStartScale;

    private readonly PhysicsThreadPool _threadPool;
    private readonly Action<int> _solveIslandAction;
    private readonly Action<int> _processNarrowPairAction;
    private float _dispatchDt;
    private float _dispatchContactErp;
    private float _dispatchJointErp;
    private bool _disposed;

    private readonly List<GenericBone> _bones;
    private readonly List<GenericRigidBody> _rigidBodies;
    private readonly List<GenericJoint> _joints;
    private readonly PhysicsState[] _states;
    private readonly Matrix4x4[] _rigidBodyOffsets;
    private readonly Matrix4x4[] _rigidBodyOffsetInverses;
    private readonly Vector3[] _localAnchorA;
    private readonly Vector3[] _localAnchorB;
    private readonly Quaternion[] _jointFrameA;
    private readonly Quaternion[] _jointFrameB;

    private readonly Vector3[] _jointLinearImpulse;
    private readonly Vector3[] _jointAngularImpulse;
    private readonly Vector3[] _jointSpringLinearImpulse;
    private readonly Vector3[] _jointSpringAngularImpulse;

    private PersistentManifold[] _manifoldPool;
    private ulong[] _manifoldKeys;
    private int _manifoldCount;
    private bool[] _manifoldActive;
    private readonly bool[] _physicsBoneFlags;

    private readonly int[] _sapSortedIndices;
    private readonly float[] _sapMinBounds;
    private readonly float[] _sapMaxBounds;
    private readonly float[] _bodyRadii;

    private readonly int[] _islandParent;
    private readonly int[] _islandRank;
    private readonly List<List<int>> _islandBodies;
    private readonly List<List<int>> _islandManifoldIndices;
    private readonly List<List<int>> _islandJointIndices;
    private readonly Dictionary<int, int> _rootToIsland;
    private int _activeIslandCount;

    private struct NarrowPhasePair
    {
        public int ManifoldIndex;
        public int BodyA;
        public int BodyB;
    }

    private NarrowPhasePair[] _narrowPairs;
    private int _narrowPairCount;

    private float _accumulator;
    private float _simulationTime;
    private readonly int _rbCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetManifoldKey(int a, int b)
    {
        return a < b ? ((ulong)a << 32) | (uint)b : ((ulong)b << 32) | (uint)a;
    }

    public GenericPhysicsEngine(List<GenericBone> bones, List<GenericRigidBody> rigidBodies, List<GenericJoint> joints)
    {
        _bones = bones ?? new List<GenericBone>();
        _rigidBodies = rigidBodies ?? new List<GenericRigidBody>();
        _joints = joints ?? new List<GenericJoint>();

        _threadPool = new PhysicsThreadPool();
        _solveIslandAction = SolveIslandDispatch;
        _processNarrowPairAction = ProcessNarrowPairDispatch;

        _rbCount = _rigidBodies.Count;
        int rbCount = _rbCount;
        _states = new PhysicsState[rbCount];
        _rigidBodyOffsets = new Matrix4x4[rbCount];
        _rigidBodyOffsetInverses = new Matrix4x4[rbCount];
        _physicsBoneFlags = new bool[_bones.Count];

        _sapSortedIndices = new int[rbCount];
        _sapMinBounds = new float[rbCount];
        _sapMaxBounds = new float[rbCount];
        _bodyRadii = new float[rbCount];

        _islandParent = new int[rbCount];
        _islandRank = new int[rbCount];
        _islandBodies = new List<List<int>>(128);
        _islandManifoldIndices = new List<List<int>>(128);
        _islandJointIndices = new List<List<int>>(128);
        for (int i = 0; i < 64; i++)
        {
            _islandBodies.Add(new List<int>(128));
            _islandManifoldIndices.Add(new List<int>(128));
            _islandJointIndices.Add(new List<int>(128));
        }
        _rootToIsland = new Dictionary<int, int>(rbCount);

        int maxManifolds = Math.Min(MaxManifolds, rbCount * rbCount / 2 + 16);
        _manifoldPool = new PersistentManifold[maxManifolds];
        _manifoldKeys = new ulong[maxManifolds];
        _manifoldActive = new bool[maxManifolds];
        _manifoldCount = 0;

        _narrowPairs = new NarrowPhasePair[maxManifolds];
        _narrowPairCount = 0;

        for (int i = 0; i < maxManifolds; i++)
        {
            _manifoldPool[i] = new PersistentManifold(0, 0);
        }

        for (int i = 0; i < rbCount; i++)
        {
            var rb = _rigidBodies[i];

            var rbRot = Matrix4x4.CreateFromYawPitchRoll(rb.Rotation.Y, rb.Rotation.X, rb.Rotation.Z);
            var rbInitialWorld = rbRot * Matrix4x4.CreateTranslation(rb.Position);

            var offset = rbInitialWorld;
            if (rb.BoneIndex >= 0 && rb.BoneIndex < _bones.Count)
            {
                var boneInitialWorld = Matrix4x4.CreateTranslation(_bones[rb.BoneIndex].Position);
                Matrix4x4.Invert(boneInitialWorld, out var boneInverse);
                offset = rbInitialWorld * boneInverse;
            }

            _rigidBodyOffsets[i] = offset;
            Matrix4x4.Invert(offset, out _rigidBodyOffsetInverses[i]);

            float invMass = (rb.PhysicsMode == 0 || rb.Mass <= 0f) ? 0f : 1f / rb.Mass;
            var initRot = Quaternion.CreateFromYawPitchRoll(rb.Rotation.Y, rb.Rotation.X, rb.Rotation.Z);

            _states[i] = new PhysicsState
            {
                Position = rb.Position,
                Rotation = initRot,
                LinearVelocity = Vector3.Zero,
                AngularVelocity = Vector3.Zero,
                InverseMass = invMass,
                Friction = rb.Friction,
                Restitution = rb.Restitution,
                LinearDamping = rb.LinearDamping,
                AngularDamping = rb.AngularDamping,
                PhysicsMode = rb.PhysicsMode,
                CollisionGroupMask = (ushort)(1 << rb.CollisionGroup),
                CollisionMask = rb.CollisionMask,
                IsSleeping = false,
                SleepTimer = 0f,
                IslandId = -1
            };

            _states[i].InverseLocalInertia = PhysicsMath.ComputeLocalInertiaInverse(rb.ShapeType, rb.ShapeSize, rb.Mass);
            _states[i].InverseWorldInertia = PhysicsMath.TransformInertia(_states[i].InverseLocalInertia, _states[i].Rotation);

            if (rb.PhysicsMode != 0 && rb.BoneIndex >= 0 && rb.BoneIndex < _physicsBoneFlags.Length)
            {
                _physicsBoneFlags[rb.BoneIndex] = true;
            }

            _sapSortedIndices[i] = i;
            _bodyRadii[i] = GetBodyRadius(rb);
        }

        int jCount = _joints.Count;
        _localAnchorA = new Vector3[jCount];
        _localAnchorB = new Vector3[jCount];
        _jointFrameA = new Quaternion[jCount];
        _jointFrameB = new Quaternion[jCount];
        _jointLinearImpulse = new Vector3[jCount];
        _jointAngularImpulse = new Vector3[jCount];
        _jointSpringLinearImpulse = new Vector3[jCount];
        _jointSpringAngularImpulse = new Vector3[jCount];

        for (int i = 0; i < jCount; i++)
        {
            var joint = _joints[i];
            var jointRot = Quaternion.CreateFromYawPitchRoll(joint.Rotation.Y, joint.Rotation.X, joint.Rotation.Z);

            if (joint.RigidBodyIndexA >= 0 && joint.RigidBodyIndexA < rbCount)
            {
                var rbA = _rigidBodies[joint.RigidBodyIndexA];
                var rbAWorldRot = Quaternion.CreateFromYawPitchRoll(rbA.Rotation.Y, rbA.Rotation.X, rbA.Rotation.Z);
                _localAnchorA[i] = Vector3.Transform(joint.Position - rbA.Position, Quaternion.Inverse(rbAWorldRot));
                _jointFrameA[i] = Quaternion.Inverse(rbAWorldRot) * jointRot;
            }
            else
            {
                _jointFrameA[i] = jointRot;
            }

            if (joint.RigidBodyIndexB >= 0 && joint.RigidBodyIndexB < rbCount)
            {
                var rbB = _rigidBodies[joint.RigidBodyIndexB];
                var rbBWorldRot = Quaternion.CreateFromYawPitchRoll(rbB.Rotation.Y, rbB.Rotation.X, rbB.Rotation.Z);
                _localAnchorB[i] = Vector3.Transform(joint.Position - rbB.Position, Quaternion.Inverse(rbBWorldRot));
                _jointFrameB[i] = Quaternion.Inverse(rbBWorldRot) * jointRot;
            }
            else
            {
                _jointFrameB[i] = jointRot;
            }
        }
    }

    public void Reset(Matrix4x4[] globalBoneTransforms)
    {
        _accumulator = 0f;
        _simulationTime = 0f;
        _manifoldCount = 0;
        _activeIslandCount = 0;

        for (int i = 0; i < _rbCount; i++)
        {
            var rb = _rigidBodies[i];
            if (rb.BoneIndex >= 0 && rb.BoneIndex < globalBoneTransforms.Length)
            {
                var rbWorld = _rigidBodyOffsets[i] * globalBoneTransforms[rb.BoneIndex];
                _states[i].Position = new Vector3(rbWorld.M41, rbWorld.M42, rbWorld.M43);
                _states[i].Rotation = ExtractRotation(rbWorld);
            }
            else
            {
                _states[i].Position = rb.Position;
                _states[i].Rotation = Quaternion.CreateFromYawPitchRoll(rb.Rotation.Y, rb.Rotation.X, rb.Rotation.Z);
            }
            _states[i].LinearVelocity = Vector3.Zero;
            _states[i].AngularVelocity = Vector3.Zero;
            _states[i].InverseWorldInertia = PhysicsMath.TransformInertia(_states[i].InverseLocalInertia, _states[i].Rotation);
            _states[i].IsSleeping = false;
            _states[i].SleepTimer = 0f;
        }

        int jCount = _joints.Count;
        for (int i = 0; i < jCount; i++)
        {
            _jointLinearImpulse[i] = Vector3.Zero;
            _jointAngularImpulse[i] = Vector3.Zero;
            _jointSpringLinearImpulse[i] = Vector3.Zero;
            _jointSpringAngularImpulse[i] = Vector3.Zero;
        }
    }

    private void ResizeManifoldCapacityIfNeeded()
    {
        int maxManifolds = Math.Min(MaxManifolds, _rbCount * _rbCount / 2 + 16);
        if (_manifoldPool.Length != maxManifolds)
        {
            int oldLength = _manifoldPool.Length;
            Array.Resize(ref _manifoldPool, maxManifolds);
            Array.Resize(ref _manifoldKeys, maxManifolds);
            Array.Resize(ref _manifoldActive, maxManifolds);
            Array.Resize(ref _narrowPairs, maxManifolds);

            for (int i = oldLength; i < maxManifolds; i++)
            {
                _manifoldPool[i] = new PersistentManifold(0, 0);
            }
            if (_manifoldCount > maxManifolds)
                _manifoldCount = maxManifolds;
        }
    }

    public void Update(Matrix4x4[] globalBoneTransforms, float deltaTime)
    {
        if (_rbCount == 0 || deltaTime <= 0f) return;
        ResizeManifoldCapacityIfNeeded();

        UpdateKinematicBodies(globalBoneTransforms, deltaTime);

        _accumulator += deltaTime;
        float maxAccum = FixedTimeStep * MaxSubSteps;
        if (_accumulator > maxAccum)
            _accumulator = maxAccum;

        int steps = 0;
        while (_accumulator >= FixedTimeStep && steps < MaxSubSteps)
        {
            StepSimulation(FixedTimeStep);
            _accumulator -= FixedTimeStep;
            steps++;
        }
    }

    public void ApplyToGlobalTransforms(Matrix4x4[] globalBoneTransforms)
    {
        for (int i = 0; i < _rbCount; i++)
        {
            var rb = _rigidBodies[i];
            if (rb.PhysicsMode == 0) continue;
            if (rb.BoneIndex < 0 || rb.BoneIndex >= globalBoneTransforms.Length) continue;

            var rbWorld = Matrix4x4.CreateFromQuaternion(_states[i].Rotation) *
                          Matrix4x4.CreateTranslation(_states[i].Position);

            var boneWorld = _rigidBodyOffsetInverses[i] * rbWorld;

            if (IsValidMatrix(ref boneWorld))
            {
                globalBoneTransforms[rb.BoneIndex] = boneWorld;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPhysicsBone(int boneIndex)
    {
        if (boneIndex < 0 || boneIndex >= _physicsBoneFlags.Length) return false;
        return _physicsBoneFlags[boneIndex];
    }

    private void UpdateKinematicBodies(Matrix4x4[] globalBoneTransforms, float deltaTime)
    {
        for (int i = 0; i < _rbCount; i++)
        {
            var rb = _rigidBodies[i];
            if (rb.BoneIndex < 0 || rb.BoneIndex >= globalBoneTransforms.Length) continue;

            if (rb.PhysicsMode == 0)
            {
                var rbWorld = _rigidBodyOffsets[i] * globalBoneTransforms[rb.BoneIndex];
                var newPos = new Vector3(rbWorld.M41, rbWorld.M42, rbWorld.M43);
                var newRot = ExtractRotation(rbWorld);

                if (deltaTime > 0f)
                {
                    float invDt = 1f / deltaTime;
                    _states[i].LinearVelocity = (newPos - _states[i].Position) * invDt;

                    var relRot = Quaternion.Inverse(_states[i].Rotation) * newRot;
                    if (relRot.W < 0) relRot = new Quaternion(-relRot.X, -relRot.Y, -relRot.Z, -relRot.W);
                    float angle = 2f * MathF.Acos(Math.Clamp(relRot.W, -1f, 1f));
                    if (angle > 1e-6f)
                    {
                        var axis = new Vector3(relRot.X, relRot.Y, relRot.Z);
                        float len = axis.Length();
                        if (len > 1e-6f)
                            _states[i].AngularVelocity = (axis / len) * (angle * invDt);
                        else
                            _states[i].AngularVelocity = Vector3.Zero;
                    }
                    else
                    {
                        _states[i].AngularVelocity = Vector3.Zero;
                    }
                }
                else
                {
                    _states[i].LinearVelocity = Vector3.Zero;
                    _states[i].AngularVelocity = Vector3.Zero;
                }

                _states[i].Position = newPos;
                _states[i].Rotation = newRot;
                _states[i].InverseWorldInertia = PhysicsMath.TransformInertia(_states[i].InverseLocalInertia, _states[i].Rotation);

                WakeNeighbors(i);
            }
        }
    }

    private void WakeNeighbors(int bodyIndex)
    {
        for (int m = 0; m < _manifoldCount; m++)
        {
            ref var manifold = ref _manifoldPool[m];
            if (manifold.PointCount == 0) continue;
            if (manifold.BodyA == bodyIndex && _states[manifold.BodyB].IsSleeping)
            {
                _states[manifold.BodyB].IsSleeping = false;
                _states[manifold.BodyB].SleepTimer = 0f;
            }
            else if (manifold.BodyB == bodyIndex && _states[manifold.BodyA].IsSleeping)
            {
                _states[manifold.BodyA].IsSleeping = false;
                _states[manifold.BodyA].SleepTimer = 0f;
            }
        }

        int jCount = _joints.Count;
        for (int j = 0; j < jCount; j++)
        {
            var joint = _joints[j];
            if (joint.RigidBodyIndexA == bodyIndex && joint.RigidBodyIndexB >= 0 && joint.RigidBodyIndexB < _states.Length && _states[joint.RigidBodyIndexB].IsSleeping)
            {
                _states[joint.RigidBodyIndexB].IsSleeping = false;
                _states[joint.RigidBodyIndexB].SleepTimer = 0f;
            }
            else if (joint.RigidBodyIndexB == bodyIndex && joint.RigidBodyIndexA >= 0 && joint.RigidBodyIndexA < _states.Length && _states[joint.RigidBodyIndexA].IsSleeping)
            {
                _states[joint.RigidBodyIndexA].IsSleeping = false;
                _states[joint.RigidBodyIndexA].SleepTimer = 0f;
            }
        }
    }

    private void StepSimulation(float dt)
    {
        _simulationTime += dt;

        IntegrateVelocities(dt);
        DetectCollisions();

        BuildIslands();

        WarmStartContacts();
        WarmStartJoints();

        float contactErp = _simulationTime < 1.0f ? 0.1f : 1.0f;
        float jointErp = 1.0f;

        if (_activeIslandCount > 4)
        {
            _dispatchDt = dt;
            _dispatchContactErp = contactErp;
            _dispatchJointErp = jointErp;
            _threadPool.Dispatch(_activeIslandCount, _solveIslandAction);
        }
        else
        {
            for (int islandIdx = 0; islandIdx < _activeIslandCount; islandIdx++)
            {
                SolveIsland(islandIdx, dt, contactErp, jointErp);
            }
        }

        ClampVelocities();
        IntegratePositions(dt);
        ApplyGroundCollision();
        UpdateSleepState(dt);
    }

    private void SolveIsland(int islandIdx, float dt, float contactErp, float jointErp)
    {
        var manifoldIndices = _islandManifoldIndices[islandIdx];
        var jointIndices = _islandJointIndices[islandIdx];

        for (int iter = 0; iter < SolverIterations; iter++)
        {
            for (int ji = 0; ji < jointIndices.Count; ji++)
            {
                int i = jointIndices[ji];
                var joint = _joints[i];
                int idxA = joint.RigidBodyIndexA;
                int idxB = joint.RigidBodyIndexB;
                if (idxA < 0 || idxA >= _states.Length) continue;
                if (idxB < 0 || idxB >= _states.Length) continue;

                PhysicsSolver.SolveJointConstraint(
                    ref _states[idxA], ref _states[idxB], joint,
                    _localAnchorA[i], _localAnchorB[i],
                    _jointFrameA[i], _jointFrameB[i], dt, jointErp,
                    ref _jointLinearImpulse[i], ref _jointAngularImpulse[i],
                    ref _jointSpringLinearImpulse[i], ref _jointSpringAngularImpulse[i]);
            }

            for (int mi = 0; mi < manifoldIndices.Count; mi++)
            {
                int mIdx = manifoldIndices[mi];
                ref var manifold = ref _manifoldPool[mIdx];
                int bodyA = manifold.BodyA;
                int bodyB = manifold.BodyB;
                for (int p = 0; p < manifold.PointCount; p++)
                {
                    PhysicsSolver.SolveContact(ref _states[bodyA], ref _states[bodyB], ref manifold.Points[p], dt, contactErp);
                }
            }
        }
    }

    private void BuildIslands()
    {
        int rbCount = _states.Length;

        for (int i = 0; i < rbCount; i++)
        {
            _islandParent[i] = i;
            _islandRank[i] = 0;
        }

        for (int m = 0; m < _manifoldCount; m++)
        {
            ref var manifold = ref _manifoldPool[m];
            if (manifold.PointCount > 0)
                UnionFind(manifold.BodyA, manifold.BodyB);
        }

        int jCount = _joints.Count;
        for (int i = 0; i < jCount; i++)
        {
            var joint = _joints[i];
            if (joint.RigidBodyIndexA >= 0 && joint.RigidBodyIndexA < rbCount &&
                joint.RigidBodyIndexB >= 0 && joint.RigidBodyIndexB < rbCount)
            {
                UnionFind(joint.RigidBodyIndexA, joint.RigidBodyIndexB);
            }
        }

        for (int i = 0; i < _activeIslandCount; i++)
        {
            _islandBodies[i].Clear();
            _islandManifoldIndices[i].Clear();
            _islandJointIndices[i].Clear();
        }

        _rootToIsland.Clear();
        int islandCount = 0;

        for (int i = 0; i < rbCount; i++)
        {
            if (_states[i].PhysicsMode == 0 && _states[i].InverseMass <= 0f) continue;

            int root = Find(i);
            if (!_rootToIsland.TryGetValue(root, out int islandIdx))
            {
                islandIdx = islandCount++;
                _rootToIsland[root] = islandIdx;

                if (_islandBodies.Count <= islandIdx)
                {
                    int needed = islandIdx + 1;
                    while (_islandBodies.Count < needed)
                    {
                        _islandBodies.Add(new List<int>(128));
                        _islandManifoldIndices.Add(new List<int>(128));
                        _islandJointIndices.Add(new List<int>(128));
                    }
                }
            }
            _states[i].IslandId = islandIdx;
            _islandBodies[islandIdx].Add(i);
        }

        for (int m = 0; m < _manifoldCount; m++)
        {
            ref var manifold = ref _manifoldPool[m];
            if (manifold.PointCount == 0) continue;

            int root = Find(manifold.BodyA);
            if (_rootToIsland.TryGetValue(root, out int islandIdx))
                _islandManifoldIndices[islandIdx].Add(m);
        }

        for (int i = 0; i < jCount; i++)
        {
            var joint = _joints[i];
            if (joint.RigidBodyIndexA < 0 || joint.RigidBodyIndexA >= rbCount) continue;
            if (joint.RigidBodyIndexB < 0 || joint.RigidBodyIndexB >= rbCount) continue;

            int root = Find(joint.RigidBodyIndexA);
            if (_rootToIsland.TryGetValue(root, out int islandIdx))
                _islandJointIndices[islandIdx].Add(i);
        }

        _activeIslandCount = islandCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(int x)
    {
        while (_islandParent[x] != x)
        {
            _islandParent[x] = _islandParent[_islandParent[x]];
            x = _islandParent[x];
        }
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UnionFind(int a, int b)
    {
        int ra = Find(a), rb = Find(b);
        if (ra == rb) return;
        if (_islandRank[ra] < _islandRank[rb]) { int t = ra; ra = rb; rb = t; }
        _islandParent[rb] = ra;
        if (_islandRank[ra] == _islandRank[rb]) _islandRank[ra]++;
    }

    private unsafe void UpdateSleepState(float dt)
    {
        float linThreshSq = SleepLinearThreshold * SleepLinearThreshold;
        float angThreshSq = SleepAngularThreshold * SleepAngularThreshold;
        int count = _states.Length;

        fixed (PhysicsState* pStates = _states)
        {
            for (int i = 0; i < count; i++)
            {
                PhysicsState* s = pStates + i;
                if (s->PhysicsMode == 0) continue;
                if (s->InverseMass <= 0f) continue;

                float linSpeedSq = s->LinearVelocity.LengthSquared();
                float angSpeedSq = s->AngularVelocity.LengthSquared();

                if (linSpeedSq < linThreshSq && angSpeedSq < angThreshSq)
                {
                    s->SleepTimer += dt;
                    if (s->SleepTimer >= SleepTimeRequired)
                    {
                        s->IsSleeping = true;
                        s->LinearVelocity = Vector3.Zero;
                        s->AngularVelocity = Vector3.Zero;
                    }
                }
                else
                {
                    s->SleepTimer = 0f;
                    s->IsSleeping = false;
                }
            }
        }
    }

    private unsafe void IntegrateVelocities(float dt)
    {
        var gravityImpulse = new Vector3(0, Gravity * dt, 0);
        float dtScaled = dt * 60f;
        int count = _states.Length;

        fixed (PhysicsState* pStates = _states)
        {
            for (int i = 0; i < count; i++)
            {
                PhysicsState* s = pStates + i;
                if (s->PhysicsMode == 0) continue;
                if (s->InverseMass <= 0f) continue;
                if (s->IsSleeping) continue;

                s->LinearVelocity += gravityImpulse;

                float linearDamp = MathF.Pow(1f - Math.Clamp(s->LinearDamping, 0f, 1f), dtScaled);
                float angularDamp = MathF.Pow(1f - Math.Clamp(s->AngularDamping, 0f, 1f), dtScaled);
                s->LinearVelocity *= linearDamp;
                s->AngularVelocity *= angularDamp;
            }
        }
    }

    private unsafe void DetectCollisions()
    {
        int rbCount = _rbCount;

        fixed (PhysicsState* pStates = _states)
        fixed (float* pMin = _sapMinBounds)
        fixed (float* pMax = _sapMaxBounds)
        fixed (float* pRadii = _bodyRadii)
        fixed (int* pSorted = _sapSortedIndices)
        {
            for (int i = 0; i < rbCount; i++)
            {
                float r = pRadii[i];
                pMin[i] = pStates[i].Position.Y - r;
                pMax[i] = pStates[i].Position.Y + r;
                pSorted[i] = i;
            }
        }

        InsertionSortSap(rbCount);

        for (int m = 0; m < _manifoldCount; m++)
            _manifoldActive[m] = false;

        _narrowPairCount = 0;

        fixed (PhysicsState* pStates = _states)
        fixed (float* pMin = _sapMinBounds)
        fixed (float* pMax = _sapMaxBounds)
        fixed (float* pRadii = _bodyRadii)
        fixed (int* pSorted = _sapSortedIndices)
        {
            for (int si = 0; si < rbCount; si++)
            {
                int i = pSorted[si];
                float maxI = pMax[i] + 0.5f;

                for (int sj = si + 1; sj < rbCount; sj++)
                {
                    int j = pSorted[sj];
                    if (pMin[j] > maxI) break;

                    if (pStates[i].PhysicsMode == 0 && pStates[j].PhysicsMode == 0) continue;
                    if (pStates[i].IsSleeping && pStates[j].IsSleeping) continue;

                    if ((pStates[i].CollisionGroupMask & pStates[j].CollisionMask) == 0 ||
                        (pStates[j].CollisionGroupMask & pStates[i].CollisionMask) == 0)
                        continue;

                    float rSum = pRadii[i] + pRadii[j] + 0.5f;
                    float distSq = (pStates[i].Position - pStates[j].Position).LengthSquared();
                    if (distSq > rSum * rSum) continue;

                    int a = Math.Min(i, j);
                    int b = Math.Max(i, j);
                    ulong key = GetManifoldKey(a, b);

                    int mIdx = FindManifold(key);
                    if (mIdx < 0)
                    {
                        mIdx = AllocateManifold(key, a, b);
                        if (mIdx < 0) continue;
                    }

                    _manifoldActive[mIdx] = true;

                    if (pStates[a].IsSleeping)
                    {
                        pStates[a].IsSleeping = false;
                        pStates[a].SleepTimer = 0f;
                    }
                    if (pStates[b].IsSleeping)
                    {
                        pStates[b].IsSleeping = false;
                        pStates[b].SleepTimer = 0f;
                    }

                    if (_narrowPairCount >= _narrowPairs.Length)
                    {
                        int newSize = Math.Min(_narrowPairs.Length * 2, _narrowPairs.Length + 1024);
                        Array.Resize(ref _narrowPairs, newSize);
                    }
                    _narrowPairs[_narrowPairCount++] = new NarrowPhasePair
                    {
                        ManifoldIndex = mIdx,
                        BodyA = a,
                        BodyB = b
                    };
                }
            }
        }

        if (_narrowPairCount >= ParallelNarrowPhaseThreshold)
        {
            _threadPool.Dispatch(_narrowPairCount, _processNarrowPairAction);
        }
        else
        {
            for (int pi = 0; pi < _narrowPairCount; pi++)
            {
                ref var pair = ref _narrowPairs[pi];
                ref var manifold = ref _manifoldPool[pair.ManifoldIndex];
                manifold.RefreshContactPoints(ref _states[pair.BodyA], ref _states[pair.BodyB]);
                PhysicsCollision.DetectCollision(pair.BodyA, _rigidBodies[pair.BodyA], ref _states[pair.BodyA],
                    pair.BodyB, _rigidBodies[pair.BodyB], ref _states[pair.BodyB], manifold);
            }
        }

        for (int m = _manifoldCount - 1; m >= 0; m--)
        {
            if (!_manifoldActive[m])
            {
                int last = _manifoldCount - 1;
                if (m != last)
                {
                    var tempManifold = _manifoldPool[m];
                    _manifoldPool[m] = _manifoldPool[last];
                    _manifoldPool[last] = tempManifold;
                    _manifoldKeys[m] = _manifoldKeys[last];
                    _manifoldActive[m] = _manifoldActive[last];
                }
                _manifoldPool[last].PointCount = 0;
                _manifoldCount--;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void InsertionSortSap(int count)
    {
        fixed (int* pSorted = _sapSortedIndices)
        fixed (float* pMin = _sapMinBounds)
        {
            for (int i = 1; i < count; i++)
            {
                int key = pSorted[i];
                float keyVal = pMin[key];
                int j = i - 1;
                while (j >= 0 && pMin[pSorted[j]] > keyVal)
                {
                    pSorted[j + 1] = pSorted[j];
                    j--;
                }
                pSorted[j + 1] = key;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindManifold(ulong key)
    {
        for (int i = 0; i < _manifoldCount; i++)
        {
            if (_manifoldKeys[i] == key) return i;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocateManifold(ulong key, int bodyA, int bodyB)
    {
        if (_manifoldCount >= _manifoldPool.Length) return -1;
        int idx = _manifoldCount++;
        _manifoldKeys[idx] = key;
        _manifoldPool[idx].BodyA = bodyA;
        _manifoldPool[idx].BodyB = bodyB;
        _manifoldPool[idx].PointCount = 0;
        _manifoldActive[idx] = true;
        return idx;
    }

    private void WarmStartContacts()
    {
        for (int m = 0; m < _manifoldCount; m++)
        {
            ref var manifold = ref _manifoldPool[m];
            int bodyA = manifold.BodyA;
            int bodyB = manifold.BodyB;
            for (int i = 0; i < manifold.PointCount; i++)
            {
                manifold.Points[i].AppliedNormalImpulse *= WarmStartScale;
                manifold.Points[i].AppliedFrictionImpulse1 *= WarmStartScale;
                manifold.Points[i].AppliedFrictionImpulse2 *= WarmStartScale;
                PhysicsSolver.WarmStartContact(ref _states[bodyA], ref _states[bodyB], ref manifold.Points[i]);
            }
        }
    }

    private void WarmStartJoints()
    {
        int jointCount = _joints.Count;
        for (int i = 0; i < jointCount; i++)
        {
            var joint = _joints[i];
            int idxA = joint.RigidBodyIndexA;
            int idxB = joint.RigidBodyIndexB;
            if (idxA < 0 || idxA >= _states.Length) continue;
            if (idxB < 0 || idxB >= _states.Length) continue;

            _jointLinearImpulse[i] *= WarmStartScale;
            _jointAngularImpulse[i] *= WarmStartScale;
            _jointSpringLinearImpulse[i] *= WarmStartScale;
            _jointSpringAngularImpulse[i] *= WarmStartScale;

            PhysicsSolver.WarmStartJoint(
                ref _states[idxA], ref _states[idxB],
                _localAnchorA[i], _localAnchorB[i],
                _jointFrameA[i], _jointFrameB[i],
                ref _jointLinearImpulse[i], ref _jointAngularImpulse[i],
                ref _jointSpringLinearImpulse[i], ref _jointSpringAngularImpulse[i]);
        }
    }

    private unsafe void IntegratePositions(float dt)
    {
        int count = _states.Length;

        fixed (PhysicsState* pStates = _states)
        {
            for (int i = 0; i < count; i++)
            {
                PhysicsState* s = pStates + i;
                if (s->PhysicsMode == 0) continue;
                if (s->InverseMass <= 0f) continue;
                if (s->IsSleeping) continue;

                s->Position += s->LinearVelocity * dt;

                float angSpeedSq = s->AngularVelocity.LengthSquared();
                if (angSpeedSq > 1e-16f)
                {
                    float angSpeed = MathF.Sqrt(angSpeedSq);
                    float angle = angSpeed * dt;
                    var axis = s->AngularVelocity * (1f / angSpeed);
                    var dq = Quaternion.CreateFromAxisAngle(axis, angle);
                    s->Rotation = Quaternion.Normalize(dq * s->Rotation);
                }

                s->InverseWorldInertia = PhysicsMath.TransformInertia(s->InverseLocalInertia, s->Rotation);
            }
        }
    }

    private unsafe void ApplyGroundCollision()
    {
        if (!EnableGroundCollision) return;
        int count = _states.Length;

        fixed (PhysicsState* pStates = _states)
        fixed (float* pRadii = _bodyRadii)
        {
            for (int i = 0; i < count; i++)
            {
                PhysicsState* s = pStates + i;
                if (s->PhysicsMode == 0) continue;
                if (s->InverseMass <= 0f) continue;

                float radius = pRadii[i];
                float minY = GroundY + radius;

                if (s->Position.Y < minY)
                {
                    s->Position.Y = minY;

                    if (s->IsSleeping)
                    {
                        s->IsSleeping = false;
                        s->SleepTimer = 0f;
                    }

                    if (s->LinearVelocity.Y < 0)
                    {
                        float bounce = -s->LinearVelocity.Y * s->Restitution * 0.3f;
                        float friction = 1f - Math.Clamp(s->Friction, 0f, 1f);
                        s->LinearVelocity = new Vector3(
                            s->LinearVelocity.X * friction,
                            bounce,
                            s->LinearVelocity.Z * friction);
                        s->AngularVelocity *= 0.9f;
                    }
                }
            }
        }
    }

    private unsafe void ClampVelocities()
    {
        float maxLinVel = _simulationTime < 1.0f ? 5.0f : 40.0f;
        float maxAngVel = _simulationTime < 1.0f ? 5.0f : 40.0f;
        float maxLinVelSq = maxLinVel * maxLinVel;
        float maxAngVelSq = maxAngVel * maxAngVel;
        int count = _states.Length;

        fixed (PhysicsState* pStates = _states)
        {
            for (int i = 0; i < count; i++)
            {
                PhysicsState* s = pStates + i;
                if (s->PhysicsMode == 0) continue;
                if (s->IsSleeping) continue;

                if (float.IsNaN(s->LinearVelocity.X) || float.IsNaN(s->LinearVelocity.Y) || float.IsNaN(s->LinearVelocity.Z))
                    s->LinearVelocity = Vector3.Zero;
                if (float.IsNaN(s->AngularVelocity.X) || float.IsNaN(s->AngularVelocity.Y) || float.IsNaN(s->AngularVelocity.Z))
                    s->AngularVelocity = Vector3.Zero;

                float linSq = s->LinearVelocity.LengthSquared();
                if (linSq > maxLinVelSq)
                    s->LinearVelocity *= maxLinVel / MathF.Sqrt(linSq);

                float angSq = s->AngularVelocity.LengthSquared();
                if (angSq > maxAngVelSq)
                    s->AngularVelocity *= maxAngVel / MathF.Sqrt(angSq);

                if (float.IsNaN(s->Position.X) || float.IsNaN(s->Position.Y) || float.IsNaN(s->Position.Z) ||
                    float.IsInfinity(s->Position.X) || float.IsInfinity(s->Position.Y) || float.IsInfinity(s->Position.Z))
                    s->Position = _rigidBodies[i].Position;

                if (float.IsNaN(s->Rotation.X) || float.IsNaN(s->Rotation.Y) || float.IsNaN(s->Rotation.Z) || float.IsNaN(s->Rotation.W) ||
                    float.IsInfinity(s->Rotation.X) || float.IsInfinity(s->Rotation.Y) || float.IsInfinity(s->Rotation.Z) || float.IsInfinity(s->Rotation.W))
                    s->Rotation = Quaternion.Identity;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidMatrix(ref Matrix4x4 m)
    {
        return !(float.IsNaN(m.M11) || float.IsInfinity(m.M11) ||
                 float.IsNaN(m.M22) || float.IsInfinity(m.M22) ||
                 float.IsNaN(m.M33) || float.IsInfinity(m.M33) ||
                 float.IsNaN(m.M41) || float.IsInfinity(m.M41) ||
                 float.IsNaN(m.M42) || float.IsInfinity(m.M42) ||
                 float.IsNaN(m.M43) || float.IsInfinity(m.M43));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetBodyRadius(GenericRigidBody rb)
    {
        return rb.ShapeType switch
        {
            0 => rb.ShapeSize.X,
            1 => MathF.Max(rb.ShapeSize.X, MathF.Max(rb.ShapeSize.Y, rb.ShapeSize.Z)),
            2 => rb.ShapeSize.X + rb.ShapeSize.Y * 0.5f,
            _ => 0.1f
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Quaternion ExtractRotation(Matrix4x4 m)
    {
        var c0 = new Vector3(m.M11, m.M12, m.M13);
        var c1 = new Vector3(m.M21, m.M22, m.M23);
        var c2 = new Vector3(m.M31, m.M32, m.M33);
        float s0 = c0.Length();
        float s1 = c1.Length();
        float s2 = c2.Length();
        if (s0 < 1e-6f) s0 = 1f;
        if (s1 < 1e-6f) s1 = 1f;
        if (s2 < 1e-6f) s2 = 1f;
        float is0 = 1f / s0, is1 = 1f / s1, is2 = 1f / s2;
        var normalized = new Matrix4x4(
            m.M11 * is0, m.M12 * is0, m.M13 * is0, 0,
            m.M21 * is1, m.M22 * is1, m.M23 * is1, 0,
            m.M31 * is2, m.M32 * is2, m.M33 * is2, 0,
            0, 0, 0, 1);
        var q = Quaternion.CreateFromRotationMatrix(normalized);
        if (float.IsNaN(q.W) || float.IsNaN(q.X) || float.IsNaN(q.Y) || float.IsNaN(q.Z))
            return Quaternion.Identity;
        float len = q.Length();
        if (len < 1e-6f) return Quaternion.Identity;
        return Quaternion.Normalize(q);
    }

    private void SolveIslandDispatch(int islandIdx)
    {
        SolveIsland(islandIdx, _dispatchDt, _dispatchContactErp, _dispatchJointErp);
    }

    private void ProcessNarrowPairDispatch(int pi)
    {
        ref var pair = ref _narrowPairs[pi];
        ref var manifold = ref _manifoldPool[pair.ManifoldIndex];
        manifold.RefreshContactPoints(ref _states[pair.BodyA], ref _states[pair.BodyB]);
        PhysicsCollision.DetectCollision(pair.BodyA, _rigidBodies[pair.BodyA], ref _states[pair.BodyA],
            pair.BodyB, _rigidBodies[pair.BodyB], ref _states[pair.BodyB], manifold);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _threadPool.Dispose();
    }
}