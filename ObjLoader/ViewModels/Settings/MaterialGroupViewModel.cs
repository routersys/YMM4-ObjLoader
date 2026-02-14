using ObjLoader.Localization;
using ObjLoader.Settings;
using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Settings
{
    public class MaterialGroupViewModel : Bindable
    {
        public string Id { get; }
        public string Title { get; }
        public ObservableCollection<MaterialItemViewModel> Items { get; } = new();
        public ActionCommand ResetGroupCommand { get; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get
            {
                if (PluginSettings.Instance.MaterialExpanderStates.TryGetValue(Id, out var state))
                    return state;
                return true;
            }
            set
            {
                if (Set(ref _isExpanded, value))
                {
                    PluginSettings.Instance.MaterialExpanderStates[Id] = value;
                    PluginSettings.Instance.Save();
                }
            }
        }

        public MaterialGroupViewModel(string id, string titleKey, Action onUpdate)
        {
            Id = id;
            Title = Texts.ResourceManager.GetString(titleKey) ?? titleKey;
            _isExpanded = IsExpanded;
            ResetGroupCommand = new ActionCommand(_ => true, _ =>
            {
                foreach (var item in Items)
                {
                    item.Reset();
                }
                onUpdate?.Invoke();
            });
        }
    }
}
