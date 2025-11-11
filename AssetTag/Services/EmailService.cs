using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssetTag.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default)
        {
            try
            {
                using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
                {
                    EnableSsl = _emailSettings.EnableSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000
                };

                var fromAddress = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
                var toAddress = new MailAddress(toEmail);

                using var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                await smtpClient.SendMailAsync(message, cancellationToken);

                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl)
        {
            var fullResetUrl = $"{resetUrl}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(resetToken)}";

            var subject = "Password Reset Request - Methodist University Asset Portal";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ background: #f9f9f9; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background: #007bff; color: white; text-decoration: none; border-radius: 4px; }}
        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Methodist University Ghana</h1>
            <p>Asset Management Portal</p>
        </div>
        <div class='content'>
            <h2>Password Reset Request</h2>
            <p>Dear User,</p>
            <p>We received a request to reset your password for the Methodist University Asset Management Portal.</p>
            <p>Click the button below to reset your password:</p>
            <p style='text-align: center;'>
                <a href='{fullResetUrl}' class='button'>Reset Password</a>
            </p>
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p><a href='{fullResetUrl}'>{fullResetUrl}</a></p>
            <p>This link will expire in 1 hour for security reasons.</p>
            <p>If you didn't request a password reset, please ignore this email.</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.Now.Year} Methodist University Ghana. All rights reserved.</p>
            <p>If you need assistance, contact us at <a href='mailto:{_emailSettings.FromEmail}'>{_emailSettings.FromEmail}</a></p>
            <p>Excellence • Morality • Service</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(email, subject, body, true);
        }
    }

    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "Methodist University Asset Portal";
        public bool EnableSsl { get; set; } = true;
    }
}