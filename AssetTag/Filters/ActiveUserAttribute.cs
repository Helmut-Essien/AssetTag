using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AssetTag.Filters
{
    public class ActiveUserAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var isActiveClaim = context.HttpContext.User.FindFirst("is_active")?.Value;

                // Check if user is active via JWT claim (NO DATABASE QUERY)
                if (isActiveClaim == null || isActiveClaim.ToLower() != "true")
                {
                    context.Result = new UnauthorizedObjectResult(new
                    {
                        error = "Account deactivated",
                        code = "ACCOUNT_DEACTIVATED",
                        message = "Your account has been deactivated. Please contact an administrator."
                    });
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}