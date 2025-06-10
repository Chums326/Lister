using System.Windows;

namespace ChumsLister.WPF.Views.Wizards
{
    public partial class AddSpecificDialog : Window
    {
        public string SpecificName { get; private set; }
        public string SpecificValue { get; private set; }

        public AddSpecificDialog()
        {
            InitializeComponent();
            txtName.Focus();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                System.Windows.MessageBox.Show("Please enter a name for the item specific", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtValue.Text))
            {
                System.Windows.MessageBox.Show("Please enter a value for the item specific", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SpecificName = txtName.Text.Trim();
            SpecificValue = txtValue.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}