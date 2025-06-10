using System.Windows;

namespace ChumsLister.WPF.Views.Wizards
{
    public partial class ImageUrlDialog : Window
    {
        public string ImageUrl { get; private set; }

        public ImageUrlDialog()
        {
            InitializeComponent();
            txtImageUrl.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ImageUrl = txtImageUrl.Text?.Trim();

            if (string.IsNullOrWhiteSpace(ImageUrl))
            {
                System.Windows.MessageBox.Show("Please enter an image URL.", "URL Required",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!System.Uri.IsWellFormedUriString(ImageUrl, System.UriKind.Absolute))
            {
                System.Windows.MessageBox.Show("Please enter a valid URL (starting with http:// or https://)",
                              "Invalid URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtImageUrl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnOk_Click(sender, e);
            }
        }
    }
}