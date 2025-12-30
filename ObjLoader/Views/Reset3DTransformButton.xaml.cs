using ObjLoader.ViewModels;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Views
{
    public partial class Reset3DTransformButton : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public Reset3DTransformButton()
        {
            InitializeComponent();
            DataContextChanged += Reset3DTransformButton_DataContextChanged;
        }

        private void Reset3DTransformButton_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is Reset3DTransformViewModel oldVm)
            {
                oldVm.BeginEdit -= ViewModel_BeginEdit;
                oldVm.EndEdit -= ViewModel_EndEdit;
            }
            if (e.NewValue is Reset3DTransformViewModel newVm)
            {
                newVm.BeginEdit += ViewModel_BeginEdit;
                newVm.EndEdit += ViewModel_EndEdit;
            }
        }

        private void ViewModel_BeginEdit(object? sender, EventArgs e)
        {
            BeginEdit?.Invoke(this, e);
        }

        private void ViewModel_EndEdit(object? sender, EventArgs e)
        {
            EndEdit?.Invoke(this, e);
        }
    }
}