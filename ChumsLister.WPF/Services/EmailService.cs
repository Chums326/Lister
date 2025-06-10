using System;
using System.Diagnostics;
using System.Net.Mail;
using System.Net;

namespace ChumsLister.WPF.Services
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _senderEmail;
        private readonly string _senderPassword;
        private readonly string _appDomain;

        public EmailService(string smtpServer, int smtpPort, string senderEmail, string senderPassword, string appDomain)
        {
            _smtpServer = smtpServer;
            _smtpPort = smtpPort;
            _senderEmail = senderEmail;
            _senderPassword = senderPassword;
            _appDomain = appDomain;
        }

        public void SendVerificationEmail(string email, string username, string verificationToken)
        {
            string subject = "Verify Your Gruffin Account";

            // Create a custom URI that will launch your app with the verification token
            string verificationLink = $"{_appDomain}verify:{verificationToken}";

            string body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                <h2>Welcome to Gruffin, {username}!</h2>
                <p>Thank you for creating an account. Please verify your email to activate your account.</p>
                
                <h3>Verification Token</h3>
                <p>Here is your verification token:</p>
                <p style='background-color: #f5f5f5; padding: 10px; border-radius: 5px; font-family: monospace; font-weight: bold; font-size: 16px;'>{verificationToken}</p>
                
                <p>To verify your account:</p>
                <ol>
                    <li>Open the Gruffin application</li>
                    <li>Click on ""Enter Verification Token"" on the login screen</li>
                    <li>Paste or type the token shown above</li>
                    <li>Click ""Verify Account""</li>
                </ol>

                <p>If you did not create an account, please ignore this email.</p>
                <p>Best regards,<br>The Gruffin Team</p>
            </body>
            </html>";

            SendEmail(email, subject, body);

            // For debugging purposes
            Debug.WriteLine($"Verification email sent to {email} with token: {verificationToken}");
        }

        public void SendWelcomeEmail(string email, string username)
        {
            string subject = "Welcome to Gruffin!";
            string body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                <h2>Welcome to Gruffin, {username}!</h2>
                <p>Your email has been verified and your account is now active.</p>
                <p>Here are some tips to get you started:</p>
                <ul>
                    <li>Create your first inventory listing</li>
                    <li>Explore the marketplace integration</li>
                    <li>Set up your profile preferences</li>
                </ul>
                <p>If you have any questions, feel free to contact our support team.</p>
                <p>Best regards,<br>The Gruffin Team</p>
            </body>
            </html>";

            SendEmail(email, subject, body);
        }

        private void SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                Debug.WriteLine($"Attempting to send email to {toEmail} using server {_smtpServer}:{_smtpPort}");
                Debug.WriteLine($"Using email account: {_senderEmail}");

                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;

                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress(_senderEmail, "Gruffin");
                        message.To.Add(new MailAddress(toEmail));
                        message.Subject = subject;
                        message.Body = body;
                        message.IsBodyHtml = true;

                        Debug.WriteLine("Connecting to SMTP server...");
                        client.Send(message);
                        Debug.WriteLine("Email sent successfully!");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending email: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // You might want to implement proper error handling/logging here
            }
        }
    }
}