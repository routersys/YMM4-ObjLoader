using ObjLoader.Rendering.Managers.Interfaces;
using System.Collections.Concurrent;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Services.Shared
{
    internal static class SharedResourceRegistry
    {
        private static readonly ConcurrentDictionary<string, ISceneDrawManager> _drawManagers = new();

        public static bool HasDrawManagers => !_drawManagers.IsEmpty;

        public static IGraphicsDevicesAndContext? SharedDevices { get; private set; }

        public static void SetSharedDevices(IGraphicsDevicesAndContext? devices)
        {
            SharedDevices = devices;
        }

        public static void RegisterDrawManager(string instanceId, ISceneDrawManager manager)
        {
            if (string.IsNullOrEmpty(instanceId) || manager == null) return;
            _drawManagers[instanceId] = manager;
        }

        public static void UnregisterDrawManager(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return;
            _drawManagers.TryRemove(instanceId, out _);
        }

        public static ISceneDrawManager? GetDrawManager(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            return _drawManagers.TryGetValue(instanceId, out var manager) ? manager : null;
        }
    }
}