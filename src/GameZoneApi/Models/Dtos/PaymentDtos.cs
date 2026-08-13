using System.ComponentModel.DataAnnotations;

namespace GameZoneApi.Models;

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
