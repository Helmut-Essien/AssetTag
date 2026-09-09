namespace Shared.Constants
{
    /// <summary>
    /// Single source of truth for email-related lifetimes shown in copy and enforced in code.
    /// </summary>
    public static class EmailConstants
    {
        /// <summary>Invitation link lifetime (DB ExpiresAt and email body must match).</summary>
        public const int InvitationExpiryDays = 7;

        /// <summary>Password-reset token lifetime (Identity TokenLifespan and email body must match).</summary>
        public const int PasswordResetExpiryHours = 1;
    }
}
