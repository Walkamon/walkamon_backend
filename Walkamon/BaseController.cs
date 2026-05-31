using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Walkamon.Controllers
{
    [ApiController]
    public class BaseController : ControllerBase
    {
        protected Guid CurrentUserId =>
            Guid.Parse(
                User.FindFirst(
                    ClaimTypes.NameIdentifier
                )!.Value
            );

        protected string CurrentEmail =>
            User.FindFirst(
                ClaimTypes.Email
            )!.Value;

        protected string CurrentRole =>
            User.FindFirst(
                ClaimTypes.Role
            )!.Value;
    }
}