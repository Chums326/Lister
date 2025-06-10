using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChumsLister.WPF.Models;

namespace ChumsLister.WPF.Services
{
    public class UserService
    {
        private readonly string _usersFilePath;
        private List<User> _users;
        private readonly EmailService _emailService;

        public UserService(string usersFilePath)
        {
            _usersFilePath = usersFilePath;
            LoadUsers();

            // Initialize EmailService with Yahoo Mail SMTP settings
            _emailService = new EmailService(
                "smtp.mail.yahoo.com",       // Yahoo Mail SMTP server
                587,                         // Yahoo Mail SMTP port
                "chumskitchenbath@yahoo.com", // Your Yahoo email
                "jeiefiqomsqokdhn",         // Your Yahoo app password (not your regular password)
                "Gruffin://"             // App protocol for verification links
            );
        }

        public Models.User GetUserByVerificationToken(string token)
        {
            try
            {
                LoadUsers();
                return _users.FirstOrDefault(u => u.VerificationToken == token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user by verification token: {ex.Message}");
                return null;
            }
        }

        private void LoadUsers()
        {
            try
            {
                if (File.Exists(_usersFilePath))
                {
                    string json = File.ReadAllText(_usersFilePath);
                    _users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
                }
                else
                {
                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(_usersFilePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    _users = new List<User>();
                    SaveUsers();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading users: {ex.Message}");
                _users = new List<User>();
            }
        }

        private void SaveUsers()
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(_usersFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_usersFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving users: {ex.Message}");
            }
        }

        public bool CreateUser(string username, string email, string password)
        {
            try
            {
                // Check if username or email already exists
                if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) ||
                                   u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                // Generate verification token
                string verificationToken = Guid.NewGuid().ToString();

                var newUser = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = password, // In real app, you should hash this
                    IsVerified = false,
                    VerificationToken = verificationToken,
                    CreatedAt = DateTime.UtcNow
                };

                _users.Add(newUser);
                SaveUsers();

                // Send verification email
                _emailService.SendVerificationEmail(email, username, verificationToken);
                Debug.WriteLine($"Verification email sent to {email} with token {verificationToken}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating user: {ex.Message}");
                return false;
            }
        }

        public bool VerifyUser(string verificationToken)
        {
            try
            {
                LoadUsers();
                var user = _users.FirstOrDefault(u => u.VerificationToken == verificationToken);

                if (user != null)
                {
                    // Mark the user as verified
                    user.IsVerified = true;
                    user.VerificationToken = null; // Clear the token once used

                    // Save the updated users list
                    SaveUsers();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error verifying user: {ex.Message}");
                return false;
            }
        }

        public User GetUserByUsername(string username)
        {
            return _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        public User GetUserByEmail(string email)
        {
            return _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }

        public bool ValidateLogin(string username, string password)
        {
            var user = GetUserByUsername(username);
            if (user == null)
                return false;

            // Check if the user is verified - add this check if it's missing
            if (!user.IsVerified)
            {
                Debug.WriteLine($"Login attempt for unverified user: {username}");
                return false;
            }

            // In a real app, you would use password hashing
            bool passwordMatches = user.PasswordHash == password;

            if (passwordMatches)
            {
                user.LastLoginAt = DateTime.UtcNow;
                SaveUsers();
            }

            return passwordMatches;
        }

        public void SaveRememberMeToken(string username)
        {
            var user = GetUserByUsername(username);
            if (user != null)
            {
                user.RememberMeToken = Guid.NewGuid().ToString();
                SaveUsers();

                // Save to application settings
                Helpers.AppSettings.RememberMeToken = user.RememberMeToken;
                Helpers.AppSettings.Username = username;
            }
        }

        public User ValidateRememberMeToken(string username, string token)
        {
            var user = GetUserByUsername(username);
            if (user != null && user.RememberMeToken == token && user.IsVerified)
            {
                user.LastLoginAt = DateTime.UtcNow;
                SaveUsers();
                return user;
            }
            return null;
        }
    }
}