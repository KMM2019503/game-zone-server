using System.ComponentModel.DataAnnotations;

namespace GameZoneApi.Models;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);
