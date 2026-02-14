using ObjLoader.Localization;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Settings
{
    public abstract class MaterialItemViewModel : Bindable
    {
        protected Action OnUpdate { get; }
        public string Label { get; }

        protected MaterialItemViewModel(string labelKey, Action onUpdate)
        {
            OnUpdate = onUpdate;
            Label = Texts.ResourceManager.GetString(labelKey) ?? labelKey;
        }

        public abstract void Reset();
    }
}
