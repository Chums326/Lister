using System.Windows;
using System.Windows.Media;
using System.Diagnostics;
using ChumsLister.WPF.ViewModels;   // for LoginViewModel
using ChumsLister.WPF.Views;

namespace ChumsLister.WPF.Views
{
    public partial class LoginPage : Window
    {
        public LoginPage()
        {
            InitializeComponent();
            this.Loaded += LoginPage_Loaded;
        }

        private void LoginPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                // 1) First, attempt auto‐login. If TryAutoLogin returns true,
                //    it has already shown MainWindow, so close this LoginPage.
                bool autoSucceeded = viewModel.TryAutoLogin();
                if (autoSucceeded)
                {
                    // Close the login window now that MainWindow is showing
                    this.Close();
                    return;
                }

                // 2) Otherwise, no valid token: continue with the normal
                //    “auto‐fill username & focus” logic.

                try
                {
                    string token = Helpers.AppSettings.RememberMeToken;
                    string savedUsername = Helpers.AppSettings.Username;
                    Debug.WriteLine($"Login page loaded. Saved username: {savedUsername}, Has token: {!string.IsNullOrEmpty(token)}");

                    if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(savedUsername))
                    {
                        viewModel.Username = savedUsername;
                        viewModel.RememberMe = true;

                        Debug.WriteLine("Auto‐filled username and checked Remember Me");

                        // Find the password box in XAML and focus it:
                        var passwordBox = FindPasswordBox();
                        passwordBox?.Focus();
                    }
                    else
                    {
                        viewModel.RememberMe = false;
                        // Focus username box
                        var usernameBox = FindUsernameBox();
                        usernameBox?.Focus();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in LoginPage_Loaded (focus logic): {ex.Message}");
                }
            }
        }

        private void VerifyToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var verificationPage = new VerificationPage();
                verificationPage.Owner = this;    // optional: make LoginPage the parent
                verificationPage.ShowDialog();    // show it modally

                // At this point, the dialog has closed. If you want to let the user
                // return to login automatically, just do nothing else (LoginPage is still open).
                // If you prefer to explicitly close LoginPage after successful verification, 
                // you can check some flag and then call this.Close(), etc.
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening verification page: {ex.Message}",
                                "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private System.Windows.Controls.PasswordBox FindPasswordBox()
        {
            return FindVisualChild<System.Windows.Controls.PasswordBox>(this);
        }

        private System.Windows.Controls.TextBox FindUsernameBox()
        {
            return FindVisualChild<System.Windows.Controls.TextBox>(this);
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}
