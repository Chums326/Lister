using System;

namespace ChumsLister.WPF.Models
{
    /// <summary>
    /// Represents a user account in the application
    /// </summary>
    public class User
    {
        /// <summary>
        /// Username for login
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// User's email address
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Password hash for authentication
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// Whether the user's email has been verified
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Token used for email verification
        /// </summary>
        public string VerificationToken { get; set; }

        /// <summary>
        /// Token used for "Remember Me" functionality
        /// </summary>
        public string RememberMeToken { get; set; }

        /// <summary>
        /// When the user account was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the user last logged in
        /// </summary>
        public DateTime? LastLoginAt { get; set; }
    }
}