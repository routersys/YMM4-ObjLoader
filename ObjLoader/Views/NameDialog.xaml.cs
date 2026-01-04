using System.Windows;

namespace ObjLoader.Views
{
    public partial class NameDialog : Window
    {
        public string ResultName { get; private set; } = "";

        public NameDialog()
        {
            InitializeComponent();
            NameBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultName = NameBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}