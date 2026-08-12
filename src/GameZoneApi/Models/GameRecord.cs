namespace GameZoneApi.Models;

/// <summary>
/// One play session: user X used machine Y for N minutes starting at T.
/// </summary>
public class GameRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid MachineId { get; set; }
    public Machine Machine { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Price charged for this session, snapshotted at write time. Kept as a column rather
    /// than recomputed from the machine so a later rate change doesn't rewrite history.
    /// </summary>
    public decimal Cost { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public DateTime EndedAt => StartedAt.AddMinutes(DurationMinutes);

    public static decimal CalculateCost(decimal hourlyRate, int durationMinutes) =>
        Math.Round(hourlyRate * durationMinutes / 60m, 2, MidpointRounding.AwayFromZero);
}
