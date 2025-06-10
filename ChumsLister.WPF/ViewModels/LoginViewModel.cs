using ChumsLister.WPF.Helpers;
using ChumsLister.WPF.Services;
using ChumsLister.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Application = System.Windows.Application;

namespace ChumsLister.WPF.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _username;
        private string _password;
        private string _email;
        private string _confirmPassword;
        private bool _isLoginMode = true;
        private string _errorMessage;
        private bool _rememberMe;

        private readonly IServiceProvider _serviceProvider;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set { _confirmPassword = value; OnPropertyChanged(); }
        }

        public bool IsLoginMode
        {
            get => _isLoginMode;
            set
            {
                _isLoginMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRegistrationMode));
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(SwitchModeText));
                ErrorMessage = string.Empty;
            }
        }

        public bool IsRegistrationMode => !_isLoginMode;
        public string ActionButtonText => IsLoginMode ? "Login" : "Create Account";
        public string SwitchModeText => IsLoginMode ? "Don't have an account? Register" : "Already have an account? Login";

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool RememberMe
        {
            get => _rememberMe;
            set { _rememberMe = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }
        public ICommand RegisterCommand { get; }
        public ICommand SwitchModeCommand { get; }

        public LoginViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            LoginCommand = new SimpleRelayCommand(Login, CanLogin);
            RegisterCommand = new SimpleRelayCommand(Register, CanRegister);
            SwitchModeCommand = new SimpleRelayCommand(SwitchMode);
        }

        private bool CanLogin() =>
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password);

        private void Login()
        {
            string dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChumsLister");
            Directory.CreateDirectory(dataFolder);
            string usersFilePath = Path.Combine(dataFolder, "users.json");

            var userService = new Services.UserService(usersFilePath);
            var user = userService.GetUserByUsername(Username);

            if (user != null && !user.IsVerified)
            {
                ErrorMessage = "Please verify your email before logging in.";
                return;
            }

            if (userService.ValidateLogin(Username, Password))
            {
                // Clear any static inventory data
                InventoryPage.ClearStaticInventory();

                // Save or clear "Remember Me"
                if (RememberMe)
                    userService.SaveRememberMeToken(Username);
                else
                    Helpers.AppSettings.ClearSavedCredentials();

                // Notify application of successful login and current user
                OnLoginSuccess(Username);

                // Navigate to main window
                ErrorMessage = string.Empty;
                

                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

                // FIXED: Close the login window instead of hiding it
                var currentWindow = Application.Current.MainWindow;
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                currentWindow.Close(); // Close instead of Hide
            }
            else
            {
                ErrorMessage = "Invalid username or password";
            }
        }

        public bool TryAutoLogin()
        {
            try
            {
                string token = Helpers.AppSettings.RememberMeToken;
                string username = Helpers.AppSettings.Username;
                Debug.WriteLine($"TryAutoLogin: Username={username}, HasToken={!string.IsNullOrEmpty(token)}");

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username))
                    return false;

                string dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ChumsLister");
                Directory.CreateDirectory(dataFolder);
                string usersFilePath = Path.Combine(dataFolder, "users.json");

                var userService = new Services.UserService(usersFilePath);
                var user = userService.ValidateRememberMeToken(username, token);
                if (user != null)
                {
                    InventoryPage.ClearStaticInventory();
                    Debug.WriteLine($"Cleared static inventory for auto-login: {username}");

                    OnLoginSuccess(username);

                    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TryAutoLogin: {ex.Message}");
                return false;
            }
        }

        private bool CanRegister() =>
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password) &&
            !string.IsNullOrWhiteSpace(Email) &&
            !string.IsNullOrWhiteSpace(ConfirmPassword) &&
            Password == ConfirmPassword;

        private void Register()
        {
            string dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChumsLister");
            Directory.CreateDirectory(dataFolder);
            string usersFilePath = Path.Combine(dataFolder, "users.json");

            var userService = new Services.UserService(usersFilePath);
            string emailToUse = string.IsNullOrEmpty(Email) ? Username : Email;

            if (userService.CreateUser(Username, emailToUse, Password))
            {
                MessageBox.Show("Account created successfully! You can now login.", "Success");
                Username = Password = Email = ConfirmPassword = string.Empty;
                IsLoginMode = true;
            }
            else
            {
                ErrorMessage = "Username or email already exists";
            }
        }

        private void SwitchMode()
        {
            IsLoginMode = !IsLoginMode;
            Username = Password = Email = ConfirmPassword = string.Empty;
        }

        private void OnLoginSuccess(string userId)
        {
            if (Application.Current is App app)
            {
                app.SetCurrentUser(userId);
                Debug.WriteLine($"OnLoginSuccess: user set to {userId}");
            }
        }
    }
}
