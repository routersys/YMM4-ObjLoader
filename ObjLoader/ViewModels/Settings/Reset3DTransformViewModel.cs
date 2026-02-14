using ObjLoader.Plugin;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Settings
{
    internal class Reset3DTransformViewModel : Bindable
    {
        private readonly ItemProperty[] _properties;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ActionCommand ResetCommand { get; }

        public Reset3DTransformViewModel(ItemProperty[] properties)
        {
            _properties = properties;

            ResetCommand = new ActionCommand(
                _ => true,
                _ => Reset()
            );
        }

        private void Reset()
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);

            foreach (var property in _properties)
            {
                if (property.PropertyOwner is ObjLoaderParameter param)
                {
                    param.X.CopyFrom(new Animation(0, -100000, 100000));
                    param.Y.CopyFrom(new Animation(0, -100000, 100000));
                    param.Z.CopyFrom(new Animation(0, -100000, 100000));
                    param.Scale.CopyFrom(new Animation(100, 0, 100000));
                    param.RotationX.CopyFrom(new Animation(0, -36000, 36000));
                    param.RotationY.CopyFrom(new Animation(0, -36000, 36000));
                    param.RotationZ.CopyFrom(new Animation(0, -36000, 36000));
                    param.Fov.CopyFrom(new Animation(45, 1, 179));
                }
            }

            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}