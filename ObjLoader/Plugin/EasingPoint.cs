using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ObjLoader.Plugin
{
    public class EasingPoint : INotifyPropertyChanged
    {
        private double _x;
        private double _y;
        private double _handleInX;
        private double _handleInY;
        private double _handleOutX;
        private double _handleOutY;

        public double X { get => _x; set => Set(ref _x, value); }
        public double Y { get => _y; set => Set(ref _y, value); }

        public double HandleInX { get => _handleInX; set => Set(ref _handleInX, value); }
        public double HandleInY { get => _handleInY; set => Set(ref _handleInY, value); }
        public double HandleOutX { get => _handleOutX; set => Set(ref _handleOutX, value); }
        public double HandleOutY { get => _handleOutY; set => Set(ref _handleOutY, value); }

        public EasingPoint() { }

        public EasingPoint(double x, double y)
        {
            _x = x; _y = y;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        public EasingPoint Clone()
        {
            return new EasingPoint
            {
                X = X,
                Y = Y,
                HandleInX = HandleInX,
                HandleInY = HandleInY,
                HandleOutX = HandleOutX,
                HandleOutY = HandleOutY
            };
        }
    }
}