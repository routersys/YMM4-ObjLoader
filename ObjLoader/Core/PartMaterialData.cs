using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ObjLoader.Core
{
    public class PartMaterialData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private double _roughness = 0.5;
        public double Roughness
        {
            get => _roughness;
            set { if (_roughness != value) { _roughness = value; OnPropertyChanged(); } }
        }

        private double _metallic = 0.0;
        public double Metallic
        {
            get => _metallic;
            set { if (_metallic != value) { _metallic = value; OnPropertyChanged(); } }
        }

        private Color _baseColor = Colors.White;
        public Color BaseColor
        {
            get => _baseColor;
            set { if (_baseColor != value) { _baseColor = value; OnPropertyChanged(); } }
        }
    }
}