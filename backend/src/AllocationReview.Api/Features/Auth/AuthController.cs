using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AllocationReview.Api.Features.Auth;

public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public sealed record CurrentUserResponse(string Email, IReadOnlyList<string> Roles);

/// <summary>Cookie sign-in built on ASP.NET Core Identity (framework authentication).</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            logger.LogWarning("Sign-in failed: unknown account");
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Sign-in blocked: account locked out. UserId {UserId}", user.Id);
            return Problem(statusCode: StatusCodes.Status423Locked,
                title: "Account temporarily locked. Try again later.");
        }

        if (!result.Succeeded)
        {
            logger.LogWarning("Sign-in failed: bad password. UserId {UserId}", user.Id);
            return InvalidCredentials();
        }

        logger.LogInformation("Sign-in succeeded. UserId {UserId}", user.Id);
        var roles = await userManager.GetRolesAsync(user);
        return Ok(new CurrentUserResponse(user.Email!, roles.ToList()));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>Lets the UI restore the session on page load and detect an expired cookie (401).</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserResponse> Me()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "";
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return Ok(new CurrentUserResponse(email, roles));
    }

    // Same response for unknown user and wrong password, so accounts cannot be enumerated.
    private ObjectResult InvalidCredentials() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
}
