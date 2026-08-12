namespace GameZoneApi.Models;

/// <summary>
/// One payment attempt or refund associated with a play session.
/// A record can have multiple payments so the system can preserve failed attempts,
/// partial payments, and refunds instead of overwriting financial history.
/// </summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecordId { get; set; }
    public GameRecord Record { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Type { get; set; } = PaymentTypes.Payment;
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = PaymentStatuses.Pending;

    public string? TransactionReference { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class PaymentTypes
{
    public const string Payment = "Payment";
    public const string Refund = "Refund";
}

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string MobileWallet = "MobileWallet";
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
