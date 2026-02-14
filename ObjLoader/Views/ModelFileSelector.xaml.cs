using System.Windows;
using System.Windows.Controls;
using ObjLoader.ViewModels.Assets;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Views
{
    public partial class ModelFileSelector : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ModelFileSelector()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ModelFileSelectorViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is ModelFileSelectorViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelFileSelectorViewModel.FilePath))
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}