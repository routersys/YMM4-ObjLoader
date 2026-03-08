using ObjLoader.Api.Core;
using Vortice.Direct2D1;

namespace ObjLoader.Api
{
    public static class ObjLoaderApi
    {
        public static event EventHandler<SceneRegistrationChangedEventArgs>? SceneRegistrationChanged
        {
            add => SceneContext.RegistrationChanged += value;
            remove => SceneContext.RegistrationChanged -= value;
        }

        public static bool TryGetScene(string instanceId, out ISceneServices? services)
        {
            return SceneContext.TryGetScene(instanceId, out services);
        }

        public static IReadOnlyList<string> GetActiveSceneIds()
        {
            return SceneContext.GetAllSceneIds();
        }

        public static ISceneServices? GetFirstScene()
        {
            return SceneContext.GetFirstScene();
        }

        public static ID2D1Image? ForceRender(string instanceId)
        {
            if (SceneContext.TryGetScene(instanceId, out var services))
            {
                if (services == null || services.IsDisposed)
                    return null;

                if (services is ObjLoaderSceneApi api)
                {
                    return api.ForceRender();
                }
            }
            return null;
        }
    }
}