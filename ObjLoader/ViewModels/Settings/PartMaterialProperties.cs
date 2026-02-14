using ObjLoader.Attributes;
using ObjLoader.Localization;
using System.Windows.Media;

namespace ObjLoader.ViewModels.Settings
{
    [MaterialGroup("Standard", nameof(Texts.Material_Group_Standard), 0)]
    public class PartMaterialProperties
    {
        private readonly Action<Action<Core.PartMaterialData>> _updateAction;

        public PartMaterialProperties(Action<Action<Core.PartMaterialData>> updateAction, Core.PartMaterialData currentData, Core.PartMaterialData defaultData)
        {
            _updateAction = updateAction;

            _roughness = currentData.Roughness;
            _metallic = currentData.Metallic;
            _baseColor = currentData.BaseColor;
        }

        private double _roughness;
        [MaterialRange("Standard", nameof(Texts.Material_Roughness), 0.0, 1.0, 0.01, 0)]
        public double Roughness
        {
            get => _roughness;
            set
            {
                _roughness = value;
                _updateAction(m => m.Roughness = value);
            }
        }

        private double _metallic;
        [MaterialRange("Standard", nameof(Texts.Material_Metallic), 0.0, 1.0, 0.01, 1)]
        public double Metallic
        {
            get => _metallic;
            set
            {
                _metallic = value;
                _updateAction(m => m.Metallic = value);
            }
        }

        private Color _baseColor;
        [MaterialColor("Standard", nameof(Texts.Material_BaseColor), 2)]
        public Color BaseColor
        {
            get => _baseColor;
            set
            {
                _baseColor = value;
                _updateAction(m => m.BaseColor = value);
            }
        }
    }
}
