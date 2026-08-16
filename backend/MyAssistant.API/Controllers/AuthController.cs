using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var validator = new MyAssistant.API.Validation.RegisterRequestValidator();
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail(validation.Errors.First().ErrorMessage, "VALIDATION"));

        var result = await _auth.RegisterAsync(request);
        return Ok(ApiResponse<AuthResult>.Ok(result, "Registration successful"));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var validator = new MyAssistant.API.Validation.LoginRequestValidator();
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Invalid email or password", "VALIDATION"));

        var result = await _auth.LoginAsync(request);
        return Ok(ApiResponse<AuthResult>.Ok(result, "Login successful"));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest body)
    {
        var result = await _auth.RefreshAsync(body.RefreshToken);
        return Ok(ApiResponse<AuthResult>.Ok(result));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest body)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auth.LogoutAsync(userId, body.RefreshToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Logged out"));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _auth.ForgotPasswordAsync(request.Email);
        return Ok(ApiResponse<object>.Ok(new { }, "If an account exists for that email, a reset link has been sent."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        await _auth.ResetPasswordAsync(request.Token, request.NewPassword);
        return Ok(ApiResponse<object>.Ok(new { }, "Password has been reset."));
    }
}

public record RefreshRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);