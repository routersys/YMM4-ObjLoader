namespace ObjLoader.Settings
{
    public interface ISettingsInitializer
    {
        bool TryInitialize(object target, object viewModel);
    }

    public static class SettingsInitializerRegistry
    {
        private static ISettingsInitializer? _instance;

        public static void Register(ISettingsInitializer initializer)
        {
            _instance = initializer;
        }

        public static bool TryInitialize(object target, object viewModel)
        {
            return _instance?.TryInitialize(target, viewModel) ?? false;
        }
    }
}