namespace Shared.Constants
{
    /// <summary>
    /// Identity role names used for authorization across API, Portal, and mobile.
    ///
    /// RBAC model:
    /// - Admin: full Portal access (dashboard, CRUD, reports, users/invitations) and privileged API.
    /// - User: mobile field clients — authenticated Sync (and related mobile auth) only; no Portal org data.
    ///
    /// Custom roles may exist in AspNetRoles but only Admin is enforced for privileged Portal/API operations.
    /// </summary>
    public static class RoleNames
    {
        public const string Admin = "Admin";
        public const string User = "User";

        /// <summary>Roles created by seed and used as invitation defaults.</summary>
        public static readonly string[] BuiltIn =
        {
            Admin,
            User
        };

        public static bool IsBuiltIn(string? role) =>
            !string.IsNullOrWhiteSpace(role) &&
            BuiltIn.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
