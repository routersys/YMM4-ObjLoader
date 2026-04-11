using ObjLoader.Api.Core;
using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Timeline;
using ObjLoader.Services.Layers;
using System.Numerics;

namespace ObjLoader.Api.Raycast
{
    internal sealed class RaycastApi : IRaycastApi
    {
        private readonly ILayerManager _layerManager;
        private readonly Func<(Matrix4x4 View, Matrix4x4 Proj, int Width, int Height)> _cameraStateProvider;

        internal RaycastApi(
            ILayerManager layerManager,
            Func<(Matrix4x4 View, Matrix4x4 Proj, int Width, int Height)> cameraStateProvider)
        {
            _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
            _cameraStateProvider = cameraStateProvider ?? throw new ArgumentNullException(nameof(cameraStateProvider));
        }

        public IReadOnlyList<RaycastHit> CastRay(Vector3 origin, Vector3 direction, RaycastFilter? filter = null)
        {
            filter ??= RaycastFilter.Default;
            var dir = Vector3.Normalize(direction);
            if (dir.LengthSquared() < 1e-6f) return Array.Empty<RaycastHit>();

            var hits = new List<RaycastHit>();

            foreach (var layer in _layerManager.Layers)
            {
                if (!layer.IsVisible) continue;
                if (filter.WorldId.HasValue && (int)layer.WorldId.GetValue(0, 1, 1) != filter.WorldId.Value) continue;
                if (string.IsNullOrEmpty(layer.FilePath)) continue;

                string cacheKey = $"export:{layer.FilePath}";
                if (!GpuResourceCache.Instance.TryGetValue(cacheKey, out var resource) || resource == null) continue;

                var world = BuildWorldMatrix(layer);
                if (!Matrix4x4.Invert(world, out var invWorld)) continue;

                var localOrigin = Vector3.Transform(origin, invWorld);
                var localDir = Vector3.TransformNormal(dir, invWorld);

                for (int i = 0; i < resource.Parts.Length; i++)
                {
                    var part = resource.Parts[i];
                    var center = part.Center;
                    float radius = resource.ModelScale > 0f ? resource.ModelScale * 0.5f : 1.0f;

                    if (!IntersectRaySphere(localOrigin, localDir, center, radius, out float dist)) continue;
                    if (dist > filter.MaxDistance) continue;

                    var hitPoint = Vector3.Transform(localOrigin + localDir * dist, world);
                    var hitNormal = Vector3.TransformNormal(Vector3.Normalize(localOrigin + localDir * dist - center), world);
                    hits.Add(new RaycastHit(new SceneObjectId(layer.Guid), i, hitPoint, Vector3.Normalize(hitNormal), dist));
                }
            }

            hits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return hits;
        }

        public IReadOnlyList<RaycastHit> CastRayFromScreenNdc(Vector2 screenNdc, RaycastFilter? filter = null)
        {
            var (view, proj, _, _) = _cameraStateProvider();

            if (!Matrix4x4.Invert(proj, out var invProj)) return Array.Empty<RaycastHit>();
            if (!Matrix4x4.Invert(view, out var invView)) return Array.Empty<RaycastHit>();

            var nearH = Vector4.Transform(new Vector4(screenNdc.X, screenNdc.Y, 0.0f, 1.0f), invProj);
            nearH /= nearH.W;
            var farH = Vector4.Transform(new Vector4(screenNdc.X, screenNdc.Y, 1.0f, 1.0f), invProj);
            farH /= farH.W;

            var nearW = Vector4.Transform(nearH, invView);
            var farW = Vector4.Transform(farH, invView);

            var origin = new Vector3(nearW.X, nearW.Y, nearW.Z);
            var direction = Vector3.Normalize(new Vector3(farW.X - nearW.X, farW.Y - nearW.Y, farW.Z - nearW.Z));

            return CastRay(origin, direction, filter);
        }

        private static Matrix4x4 BuildWorldMatrix(LayerData layer)
        {
            float scale = (float)(layer.Scale.GetValue(0, 1, 1) / 100.0);
            float rx = (float)(layer.RotationX.GetValue(0, 1, 1) * MathF.PI / 180f);
            float ry = (float)(layer.RotationY.GetValue(0, 1, 1) * MathF.PI / 180f);
            float rz = (float)(layer.RotationZ.GetValue(0, 1, 1) * MathF.PI / 180f);
            float tx = (float)layer.X.GetValue(0, 1, 1);
            float ty = (float)layer.Y.GetValue(0, 1, 1);
            float tz = (float)layer.Z.GetValue(0, 1, 1);
            return Matrix4x4.CreateScale(scale)
                 * Matrix4x4.CreateRotationZ(rz)
                 * Matrix4x4.CreateRotationX(rx)
                 * Matrix4x4.CreateRotationY(ry)
                 * Matrix4x4.CreateTranslation(tx, ty, tz);
        }

        private static bool IntersectRaySphere(Vector3 origin, Vector3 dir, Vector3 center, float radius, out float t)
        {
            var oc = origin - center;
            float b = Vector3.Dot(oc, dir);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float disc = b * b - c;
            if (disc < 0) { t = 0; return false; }
            t = -b - MathF.Sqrt(disc);
            if (t < 0) t = -b + MathF.Sqrt(disc);
            return t >= 0;
        }
    }
}