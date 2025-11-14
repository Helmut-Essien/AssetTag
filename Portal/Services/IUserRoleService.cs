using System.Security.Claims;

namespace Portal.Services
{
    public interface IUserRoleService
    {
        List<string> GetUserRoles();
        bool IsInRole(string role);
    }
}
