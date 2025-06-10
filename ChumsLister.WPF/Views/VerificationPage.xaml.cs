using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Extensions.DependencyInjection;

namespace ChumsLister.WPF.Views
{
    public partial class VerificationPage : Window
    {
        private string _token;

        public VerificationPage(string token)
        {
            InitializeComponent();
            _token = token;

            if (!string.IsNullOrEmpty(_token))
            {
                TokenTextBox.Text = _token;
                VerifyToken(_token);
            }
        }

        public VerificationPage()
        {
            InitializeComponent();
            MessageTextBlock.Text = "Please enter your verification token to activate your account.";
        }

        private void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            string token = TokenTextBox.Text.Trim();
            if (string.IsNullOrEmpty(token))
            {
                MessageBox.Show("Please enter a verification token.", "Verification Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            VerifyToken(token);
        }

        private void VerifyToken(string token)
        {
            try
            {
                Debug.WriteLine($"Attempting to verify token: {token}");

                string dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ChumsLister");
                Directory.CreateDirectory(dataFolder);
                string usersFilePath = Path.Combine(dataFolder, "users.json");

                Debug.WriteLine($"Users file path: {usersFilePath}");

                var userService = new Services.UserService(usersFilePath);
                if (userService.VerifyUser(token))
                {
                    Debug.WriteLine("Verification successful");
                    MessageTextBlock.Text = "Your email has been verified successfully! You can now login to your account.";
                    TokenTextBox.IsEnabled = false;
                    VerifyButton.IsEnabled = false;

                    try
                    {
                        var user = userService.GetUserByVerificationToken(token);
                        if (user != null)
                        {
                            var emailService = App.ServiceProvider.GetService(typeof(Services.EmailService)) as Services.EmailService;
                            emailService?.SendWelcomeEmail(user.Email, user.Username);
                            Debug.WriteLine($"Welcome email sent to {user.Email}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error sending welcome email: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine("Verification failed - invalid token");
                    MessageTextBlock.Text = "Invalid or expired verification token. Please check the token and try again.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during verification: {ex.Message}");
                MessageTextBlock.Text = $"Error verifying account: {ex.Message}";
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var loginViewModel = App.ServiceProvider.GetRequiredService<ViewModels.LoginViewModel>();
                loginViewModel.IsLoginMode = true;

                var loginWindow = new LoginPage { DataContext = loginViewModel };
                loginWindow.Show();

                this.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating back to login: {ex.Message}");
                MessageBox.Show("Error returning to login screen. Please restart the application.",
                    "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
