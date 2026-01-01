using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Views
{
    public partial class CameraWindowButton : UserControl, IPropertyEditorControl
    {
#pragma warning disable CS0067
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;
#pragma warning restore CS0067

        public CameraWindowButton()
        {
            InitializeComponent();
        }
    }
}