using System.Threading;
using System.Threading.Tasks;

namespace AssetTag.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default);
        Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl);
        Task<bool> SendInvitationEmailAsync(string email, string invitationToken, string invitationUrl, string invitedByUserName);
    }
}