// Services/UserRoleService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Portal.Services
{
    public class UserRoleService : IUserRoleService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserRoleService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public List<string> GetUserRoles()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return new List<string>();

            // Extract roles from the authenticated user's claims
            // These should be populated from the JWT token during authentication
            return user.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();
        }

        public bool IsInRole(string role)
        {
            var roles = GetUserRoles();
            return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }
    }
}