namespace Shared.Constants
{
    /// <summary>
    /// Identity role names used for authorization across API, Portal, and mobile.
    ///
    /// RBAC model:
    /// - Admin: full Portal access (dashboard, CRUD, reports, users/invitations) and privileged API.
    /// - User: mobile field clients — Sync (assets), location list/create/update APIs, and mobile auth;
    ///   no Portal org admin UI (categories/departments/users/reports). Location delete remains Admin-only.
    /// - Field APIs that allow both roles use <see cref="AdminOrUser"/> (excludes custom roles).
    ///
    /// Custom roles may exist in AspNetRoles but only Admin is enforced for privileged Portal/API operations.
    /// </summary>
    public static class RoleNames
    {
        public const string Admin = "Admin";
        public const string User = "User";

        /// <summary>
        /// Comma-separated roles for <c>[Authorize(Roles = ...)]</c> on field + admin APIs
        /// (locations list/create/update, sync). Custom roles are excluded by design.
        /// </summary>
        public const string AdminOrUser = Admin + "," + User;

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
