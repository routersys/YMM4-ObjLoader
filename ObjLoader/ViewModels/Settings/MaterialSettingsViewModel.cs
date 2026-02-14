using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Settings
{
    public class MaterialSettingsViewModel : Bindable
    {
        public ObservableCollection<MaterialGroupViewModel> Groups { get; } = new();

        public MaterialSettingsViewModel(PartMaterialProperties target, Action onUpdate)
        {
            PartMaterialPropertiesFactory.CreateGroups(this, target, onUpdate);
        }
    }
}
