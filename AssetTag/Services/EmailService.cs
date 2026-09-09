using System.Net;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Shared.Constants;

namespace AssetTag.Services
{
    public class EmailService : IEmailService
    {
        // Aligned with Portal authLayout.css (logo blues), not Bootstrap defaults.
        private const string BrandPrimary = "#2b6cb0";
        private const string BrandPrimaryDark = "#1e3a8a";
        private const string ContentBg = "#f8fafc";
        private const string TextColor = "#1e293b";
        private const string MutedColor = "#64748b";
        private const string CalloutBg = "#eff6ff";
        private const string CalloutBorder = "#bfdbfe";

        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            bool isHtml = false,
            CancellationToken cancellationToken = default)
        {
            var html = isHtml ? body : null;
            var plain = isHtml ? HtmlToPlainFallback(body) : body;
            return await SendMimeAsync(toEmail, subject, plain, html, cancellationToken);
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl)
        {
            var fullResetUrl = $"{resetUrl}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(resetToken)}";
            var safeUrl = WebUtility.HtmlEncode(fullResetUrl);
            var expiryLabel = FormatHours(EmailConstants.PasswordResetExpiryHours);

            var subject = "Reset your Asset Portal password";
            var preheader = $"Use this link within {expiryLabel} to choose a new password.";
            var title = "Reset your password";

            var htmlContent = $@"
            <h2 style=""margin:0 0 16px;font-size:20px;line-height:1.3;color:{TextColor};"">{WebUtility.HtmlEncode(title)}</h2>
            <p style=""margin:0 0 16px;color:{TextColor};"">We received a request to reset your password for the Methodist University Asset Management Portal.</p>
            <p style=""margin:0 0 8px;color:{TextColor};"">Use the button below to choose a new password:</p>
            {BuildCtaButton(safeUrl, "Reset password")}
            {BuildExpiryCallout($"This link expires in {expiryLabel}.")}
            <p style=""margin:16px 0 0;font-size:14px;color:{MutedColor};"">If the button does not work, open this link: <a href=""{safeUrl}"" style=""color:{BrandPrimary};"">Open reset page</a></p>
            <p style=""margin:16px 0 0;color:{TextColor};"">If you did not request a password reset, you can ignore this email.</p>
            <p style=""margin:8px 0 0;color:{TextColor};"">We will never ask for your password by email.</p>";

            var plainContent = $@"Reset your password

We received a request to reset your password for the Methodist University Asset Management Portal.

Reset your password:
{fullResetUrl}

This link expires in {expiryLabel}.

If you did not request a password reset, you can ignore this email.

We will never ask for your password by email.";

            return await SendMimeAsync(
                email,
                subject,
                WrapPlain(plainContent),
                WrapHtml(title, preheader, htmlContent),
                CancellationToken.None);
        }

        public async Task<bool> SendInvitationEmailAsync(
            string email,
            string invitationToken,
            string invitationUrl,
            string invitedByUserName,
            string? role = null)
        {
            var fullInvitationUrl = $"{invitationUrl}?token={Uri.EscapeDataString(invitationToken)}";
            var safeUrl = WebUtility.HtmlEncode(fullInvitationUrl);
            var inviter = string.IsNullOrWhiteSpace(invitedByUserName) ? "An administrator" : invitedByUserName.Trim();
            var safeInviter = WebUtility.HtmlEncode(inviter);
            var expiryLabel = FormatDays(EmailConstants.InvitationExpiryDays);
            var access = ResolveInvitationAccessCopy(role);

            var subject = BuildInvitationSubject(inviter, access.SubjectAudience);
            var preheader = access.Preheader(expiryLabel);
            var title = "You're invited";

            var htmlContent = $@"
            <h2 style=""margin:0 0 16px;font-size:20px;line-height:1.3;color:{TextColor};"">{WebUtility.HtmlEncode(title)}</h2>
            <p style=""margin:0 0 16px;color:{TextColor};""><strong>{safeInviter}</strong> invited you to the Methodist University Asset Management system.</p>
            <p style=""margin:0 0 16px;color:{TextColor};"">{WebUtility.HtmlEncode(access.BodyLine)}</p>
            <p style=""margin:0 0 8px;color:{TextColor};"">Create your account to get started:</p>
            {BuildCtaButton(safeUrl, "Accept invitation")}
            {BuildExpiryCallout($"This invitation expires in {expiryLabel}.")}
            <p style=""margin:16px 0 0;font-size:14px;color:{MutedColor};"">If the button does not work, open this link: <a href=""{safeUrl}"" style=""color:{BrandPrimary};"">Open invitation page</a></p>
            <p style=""margin:16px 0 0;color:{TextColor};"">If you did not expect this invitation, you can ignore this email.</p>";

            var plainContent = $@"You're invited

{inviter} invited you to the Methodist University Asset Management system.

{access.BodyLine}

Accept your invitation:
{fullInvitationUrl}

This invitation expires in {expiryLabel}.

If you did not expect this invitation, you can ignore this email.";

            return await SendMimeAsync(
                email,
                subject,
                WrapPlain(plainContent),
                WrapHtml(title, preheader, htmlContent),
                CancellationToken.None);
        }

        /// <summary>
        /// Maps invitation role to accurate access wording (see RoleNames RBAC).
        /// User = mobile field sync; Admin = Portal (+ mobile sync as any authenticated user).
        /// </summary>
        private static InvitationAccessCopy ResolveInvitationAccessCopy(string? role)
        {
            if (string.Equals(role, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return new InvitationAccessCopy(
                    SubjectAudience: "the Asset Portal",
                    BodyLine: "This Admin account is for the Asset Management Portal (dashboard, assets, users) and the MUG ASSETS mobile app. After you create your account in the browser, sign in on the Portal or mobile app.",
                    Preheader: expiry => $"Create your Admin account within {expiry} to access the Portal.");
            }

            if (string.IsNullOrWhiteSpace(role)
                || string.Equals(role, RoleNames.User, StringComparison.OrdinalIgnoreCase))
            {
                return new InvitationAccessCopy(
                    SubjectAudience: "the MUG ASSETS app",
                    BodyLine: "This account is for the MUG ASSETS mobile app, where you can sync and update assets in the field. It does not include Asset Management Portal admin access. Create your account in the browser, then sign in on the mobile app.",
                    Preheader: expiry => $"Create your mobile account within {expiry}, then sign in on the app.");
            }

            // Custom / unexpected roles: do not invent Portal or mobile capabilities.
            return new InvitationAccessCopy(
                SubjectAudience: "AssetTag",
                BodyLine: "This account will use the permissions assigned with your invitation. Create your account in the browser; your administrator can explain what you can access afterward.",
                Preheader: expiry => $"Create your account within {expiry} to get started.");
        }

        private readonly record struct InvitationAccessCopy(
            string SubjectAudience,
            string BodyLine,
            Func<string, string> Preheader);

        private async Task<bool> SendMimeAsync(
            string toEmail,
            string subject,
            string plainBody,
            string? htmlBody,
            CancellationToken cancellationToken)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    string.IsNullOrWhiteSpace(_emailSettings.FromName)
                        ? "Methodist University Asset Portal"
                        : _emailSettings.FromName,
                    _emailSettings.FromEmail));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;

                var builder = new BodyBuilder
                {
                    TextBody = plainBody
                };
                if (!string.IsNullOrWhiteSpace(htmlBody))
                {
                    builder.HtmlBody = htmlBody;
                }

                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                var secureSocketOptions = _emailSettings.EnableSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

                await client.ConnectAsync(
                    _emailSettings.SmtpServer,
                    _emailSettings.Port,
                    secureSocketOptions,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(_emailSettings.Username))
                {
                    await client.AuthenticateAsync(
                        _emailSettings.Username,
                        _emailSettings.Password,
                        cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {ToEmail}", toEmail);
                return false;
            }
        }

        private string WrapHtml(string heading, string preheader, string contentHtml)
        {
            var year = DateTime.UtcNow.Year;
            var safeHeading = WebUtility.HtmlEncode(heading);
            var safePreheader = WebUtility.HtmlEncode(preheader);
            var supportHtml = BuildSupportHtml();

            // Table-based single-column shell for Outlook / client reliability.
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <meta http-equiv=""x-ua-compatible"" content=""ie=edge"" />
  <title>{safeHeading}</title>
</head>
<body style=""margin:0;padding:0;background:#ffffff;font-family:Arial,Helvetica,sans-serif;line-height:1.6;color:{TextColor};"">
  <div style=""display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;"">
    {safePreheader}{PreheaderPad}
  </div>
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;background:#ffffff;"">
    <tr>
      <td align=""center"" style=""padding:24px 12px;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""border-collapse:collapse;max-width:600px;width:100%;"">
          <tr>
            <td align=""center"" bgcolor=""{BrandPrimary}"" style=""background:{BrandPrimary};padding:24px 20px;color:#ffffff;"">
              <h1 style=""margin:0;font-size:22px;line-height:1.3;color:#ffffff;font-weight:bold;"">Methodist University Ghana</h1>
              <p style=""margin:8px 0 0;font-size:14px;line-height:1.4;color:#ffffff;"">Asset Management Portal</p>
            </td>
          </tr>
          <tr>
            <td bgcolor=""{ContentBg}"" style=""background:{ContentBg};padding:24px 20px;color:{TextColor};font-size:16px;"">
              {contentHtml}
            </td>
          </tr>
          <tr>
            <td align=""center"" style=""padding:20px;font-size:12px;line-height:1.5;color:{MutedColor};"">
              <p style=""margin:0 0 8px;"">&copy; {year} Methodist University Ghana. All rights reserved.</p>
              <p style=""margin:0 0 8px;"">{supportHtml}</p>
              <p style=""margin:0;color:{BrandPrimaryDark};"">Excellence &bull; Morality &bull; Service</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        private string WrapPlain(string content)
        {
            var year = DateTime.UtcNow.Year;
            var sb = new StringBuilder();
            sb.AppendLine("Methodist University Ghana — Asset Management Portal");
            sb.AppendLine();
            sb.AppendLine(content.Trim());
            sb.AppendLine();
            sb.AppendLine($"© {year} Methodist University Ghana. All rights reserved.");
            sb.AppendLine(BuildSupportPlain());
            sb.AppendLine("Excellence • Morality • Service");
            return sb.ToString();
        }

        private string BuildSupportHtml()
        {
            var support = ResolveSupportEmail();
            if (support is null)
            {
                return "Need help? Contact your administrator.";
            }

            var safe = WebUtility.HtmlEncode(support);
            return $"Need help? Contact us at <a href=\"mailto:{safe}\" style=\"color:{BrandPrimary};\">{safe}</a>";
        }

        private string BuildSupportPlain()
        {
            var support = ResolveSupportEmail();
            return support is null
                ? "Need help? Contact your administrator."
                : $"Need help? Contact us at {support}";
        }

        /// <summary>
        /// Prefer SupportEmail; fall back to FromEmail unless it looks like a no-reply mailbox.
        /// </summary>
        private string? ResolveSupportEmail()
        {
            if (!string.IsNullOrWhiteSpace(_emailSettings.SupportEmail))
                return _emailSettings.SupportEmail.Trim();

            var from = _emailSettings.FromEmail?.Trim();
            if (string.IsNullOrWhiteSpace(from))
                return null;

            if (LooksLikeNoReply(from))
                return null;

            return from;
        }

        private static bool LooksLikeNoReply(string email)
        {
            var local = email.Split('@')[0];
            return local.Contains("noreply", StringComparison.OrdinalIgnoreCase)
                   || local.Contains("no-reply", StringComparison.OrdinalIgnoreCase)
                   || local.Contains("donotreply", StringComparison.OrdinalIgnoreCase)
                   || local.Contains("do-not-reply", StringComparison.OrdinalIgnoreCase);
        }

        // Pads inbox preview so clients do not leak the header/body after a short preheader.
        private const string PreheaderPad =
            "&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;"
            + "&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;"
            + "&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;";

        private static string BuildCtaButton(string safeUrl, string label)
        {
            var safeLabel = WebUtility.HtmlEncode(label);
            // Padding on the <td> (not only the <a>) improves hit area rendering in Outlook.
            return $@"
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" align=""center"" style=""border-collapse:collapse;margin:24px auto;"">
              <tr>
                <td align=""center"" bgcolor=""{BrandPrimary}"" style=""background:{BrandPrimary};border-radius:4px;padding:12px 24px;"">
                  <a href=""{safeUrl}"" style=""font-size:16px;font-weight:bold;color:#ffffff;text-decoration:none;"">{safeLabel}</a>
                </td>
              </tr>
            </table>";
        }

        private static string BuildExpiryCallout(string text)
        {
            var safe = WebUtility.HtmlEncode(text);
            return $@"
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;margin:0 0 8px;"">
              <tr>
                <td style=""background:{CalloutBg};border:1px solid {CalloutBorder};border-radius:4px;padding:12px 14px;color:{BrandPrimaryDark};font-size:14px;font-weight:bold;"">
                  {safe}
                </td>
              </tr>
            </table>";
        }

        private static string BuildInvitationSubject(string inviter, string audience)
        {
            // Keep subject single-line and reasonably short for mobile inboxes.
            var cleaned = string.Join(' ', inviter.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (cleaned.Length > 32)
                cleaned = cleaned[..29] + "...";

            return $"{cleaned} invited you to {audience}";
        }

        private static string FormatHours(int hours) =>
            hours == 1 ? "1 hour" : $"{hours} hours";

        private static string FormatDays(int days) =>
            days == 1 ? "1 day" : $"{days} days";

        private static string HtmlToPlainFallback(string html)
        {
            return WebUtility.HtmlDecode(
                System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " "));
        }

        public class EmailSettings
        {
            public string SmtpServer { get; set; } = "smtp.gmail.com";
            public int Port { get; set; } = 587;
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string FromEmail { get; set; } = string.Empty;
            public string FromName { get; set; } = "Methodist University Asset Portal";
            /// <summary>Optional help desk address shown in footers. When empty, FromEmail is used unless it looks like no-reply.</summary>
            public string SupportEmail { get; set; } = string.Empty;
            public bool EnableSsl { get; set; } = true;
        }
    }
}
