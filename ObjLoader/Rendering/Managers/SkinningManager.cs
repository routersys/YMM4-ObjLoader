using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using ObjLoader.Rendering.Core.Resolvers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Rendering.Processors;
using ObjLoader.Services.Mmd.Animation;
using System.Runtime.CompilerServices;
using Vortice.Direct3D11;
using System.Buffers;

namespace ObjLoader.Rendering.Managers
{
    public sealed class SkinningManager : ISkinningManager
    {
        private readonly Dictionary<string, SkinningState> _skinningStates = new();
        private readonly List<string> _staleGuidsBuffer = new();
        private readonly ID3D11Device _device;

        public SkinningManager(ID3D11Device device)
        {
            _device = device;
        }

        public void RegisterSkinningState(string guid, string filePath, ObjVertex[] vertices, VertexBoneWeight[] boneWeights)
        {
            if (_skinningStates.TryGetValue(guid, out var existing))
            {
                DisposeSkinningStateResources(existing);
            }

            _skinningStates[guid] = new SkinningState
            {
                FilePath = filePath,
                OriginalVertices = vertices,
                BoneWeights = boneWeights
            };
        }

        public void RemoveSkinningState(string guid)
        {
            if (!_skinningStates.Remove(guid, out var state))
            {
                return;
            }
            DisposeSkinningStateResources(state);
        }

        public void CleanupStaleStates(HashSet<string> activeGuids)
        {
            if (_skinningStates.Count == 0) return;

            _staleGuidsBuffer.Clear();
            foreach (var key in _skinningStates.Keys)
            {
                if (!activeGuids.Contains(key))
                {
                    _staleGuidsBuffer.Add(key);
                }
            }

            foreach (var key in _staleGuidsBuffer)
            {
                RemoveSkinningState(key);
            }
        }

        public ID3D11Buffer? GetOverrideVertexBuffer(string guid)
        {
            return _skinningStates.TryGetValue(guid, out var state) ? state.DynamicVB : null;
        }

        public void ProcessSkinning(string guid, string filePath, BoneAnimator? animator, double currentTime)
        {
            if (!_skinningStates.TryGetValue(guid, out var skinState)
                || !string.Equals(skinState.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (animator == null || !filePath.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase))
            {
                ClearDynamicBuffer(skinState);
                return;
            }
            ApplySkinning(animator, skinState, currentTime);
        }

        private static void ClearDynamicBuffer(SkinningState state)
        {
            if (!state.UseGpuSkinning)
            {
                state.DynamicVB?.Dispose();
            }
            state.DynamicVB = null;
            state.UseGpuSkinning = false;
        }

        private unsafe void ApplySkinning(BoneAnimator animator, SkinningState skinState, double currentTime)
        {
            var boneTransforms = animator.ComputeBoneTransforms(currentTime);
            try
            {
                var device = _device;
                var context = device.ImmediateContext;

                if (TryApplyGpuSkinning(skinState, device, context, boneTransforms))
                {
                    return;
                }

                ApplyCpuSkinningFallback(skinState, device, context, boneTransforms);
            }
            catch
            {
            }
            finally
            {
                ArrayPool<System.Numerics.Matrix4x4>.Shared.Return(boneTransforms);
            }
        }

        private static bool TryApplyGpuSkinning(
            SkinningState skinState,
            ID3D11Device device,
            ID3D11DeviceContext context,
            System.Numerics.Matrix4x4[] boneTransforms)
        {
            if (skinState.GpuProcessor == null)
            {
                skinState.GpuProcessor = new GpuSkinningProcessor();
                try
                {
                    skinState.GpuProcessor.Initialize(device);
                }
                catch
                {
                    skinState.GpuProcessor.Dispose();
                    skinState.GpuProcessor = null;
                    return false;
                }
            }

            if (!skinState.GpuProcessor.IsAvailable)
            {
                return false;
            }

            try
            {
                var gpuResult = skinState.GpuProcessor.ApplySkinning(
                    device, context, skinState.OriginalVertices, skinState.BoneWeights, boneTransforms);

                if (gpuResult == null)
                {
                    return false;
                }

                if (!skinState.UseGpuSkinning)
                {
                    skinState.DynamicVB?.Dispose();
                }

                skinState.DynamicVB = gpuResult;
                skinState.UseGpuSkinning = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static unsafe void ApplyCpuSkinningFallback(
            SkinningState skinState,
            ID3D11Device device,
            ID3D11DeviceContext context,
            System.Numerics.Matrix4x4[] boneTransforms)
        {
            var skinnedVerts = VmdMotionApplier.ApplySkinning(
                skinState.OriginalVertices, skinState.BoneWeights, boneTransforms);
            try
            {
                int vertexCount = skinState.OriginalVertices.Length;
                int requiredSize = vertexCount * Unsafe.SizeOf<ObjVertex>();

                if (skinState.UseGpuSkinning)
                {
                    skinState.DynamicVB = null;
                    skinState.UseGpuSkinning = false;
                }

                if (skinState.DynamicVB != null && skinState.DynamicVB.Description.ByteWidth != requiredSize)
                {
                    skinState.DynamicVB.Dispose();
                    skinState.DynamicVB = null;
                }

                skinState.DynamicVB ??= device.CreateBuffer(
                    new BufferDescription(requiredSize, BindFlags.VertexBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));

                var mapped = context.Map(skinState.DynamicVB, MapMode.WriteDiscard);
                fixed (ObjVertex* pVerts = skinnedVerts)
                {
                    Buffer.MemoryCopy(pVerts, (void*)mapped.DataPointer, mapped.RowPitch, requiredSize);
                }
                context.Unmap(skinState.DynamicVB, 0);
            }
            finally
            {
                ArrayPool<ObjVertex>.Shared.Return(skinnedVerts);
            }
        }

        private static void DisposeSkinningStateResources(SkinningState state)
        {
            state.GpuProcessor?.Dispose();
            state.GpuProcessor = null;
            if (!state.UseGpuSkinning)
            {
                state.DynamicVB?.Dispose();
            }
            state.DynamicVB = null;
        }

        public void Dispose()
        {
            foreach (var kvp in _skinningStates)
            {
                DisposeSkinningStateResources(kvp.Value);
            }
            _skinningStates.Clear();
        }
    }
}