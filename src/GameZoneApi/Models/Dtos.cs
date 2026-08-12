using System.ComponentModel.DataAnnotations;

namespace GameZoneApi.Models;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

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

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);

public record MachineResponse(
    Guid Id,
    string Name,
    string Specs,
    decimal HourlyRate,
    bool IsActive)
{
    public static MachineResponse From(Machine machine) =>
        new(machine.Id, machine.Name, machine.Specs, machine.HourlyRate, machine.IsActive);
}

public record CreateRecordRequest(
    [Required] Guid MachineId,
    [Required] DateTime StartedAt,
    [Range(1, 1440)] int DurationMinutes,
    [StringLength(500)] string? Notes = null)
{
    /// <summary>Admin-only: book the session for someone else. Ignored for non-admins.</summary>
    public Guid? UserId { get; init; }
}

public record UpdateRecordRequest(
    [Required] Guid MachineId,
    [Required] DateTime StartedAt,
    [Range(1, 1440)] int DurationMinutes,
    [StringLength(500)] string? Notes = null);

public record RecordResponse(
    Guid Id,
    Guid UserId,
    string UserFullName,
    Guid MachineId,
    string MachineName,
    DateTime StartedAt,
    DateTime EndedAt,
    int DurationMinutes,
    decimal Cost,
    string? Notes,
    DateTime CreatedAt)
{
    // Requires the User and Machine navigations to be loaded (Include or a fresh Load).
    public static RecordResponse From(GameRecord r) =>
        new(r.Id, r.UserId, r.User.FullName, r.MachineId, r.Machine.Name,
            r.StartedAt, r.EndedAt, r.DurationMinutes, r.Cost, r.Notes, r.CreatedAt);
}

public record CreatePaymentRequest(
    [Required] Guid RecordId,
    [Range(typeof(decimal), "0.01", "99999999.99")]
    decimal Amount,
    [Required, AllowedValues(
        PaymentMethods.Cash,
        PaymentMethods.Card,
        PaymentMethods.MobileWallet)]
    string Method,
    [StringLength(100)] string? TransactionReference = null,
    [StringLength(500)] string? Notes = null);

public record PaymentResponse(
    Guid Id,
    Guid RecordId,
    decimal Amount,
    string Type,
    string Method,
    string Status,
    string? TransactionReference,
    DateTime? CompletedAt,
    string? Notes,
    DateTime CreatedAt)
{
    public static PaymentResponse From(Payment payment) =>
        new(payment.Id, payment.RecordId, payment.Amount, payment.Type,
            payment.Method, payment.Status, payment.TransactionReference,
            payment.CompletedAt, payment.Notes, payment.CreatedAt);
}
