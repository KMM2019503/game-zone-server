using System.ComponentModel.DataAnnotations;

namespace GameZoneApi.Models;

public record UpdateUserRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt)
{
    public static UserResponse From(User user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.CreatedAt, user.LastLoginAt);
}
