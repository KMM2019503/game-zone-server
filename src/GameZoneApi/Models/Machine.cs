namespace GameZoneApi.Models;

/// <summary>
/// A gaming rig that sessions get booked against. Reference data only - the rows are
/// seeded from <see cref="Seed"/> and there is no write API for them.
/// </summary>
public class Machine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Specs { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<GameRecord> Records { get; set; } = new List<GameRecord>();

    // Fixed ids, not Guid.NewGuid(), so EF's HasData produces the same rows on every
    // migration. A generated id would make each "dotnet ef migrations add" emit a
    // delete-then-insert for the whole table.
    public static readonly Machine[] Seed =
    [
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Rig-01",
            Specs = "RTX 4090 / i9-14900K / 64GB",
            HourlyRate = 8.00m
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Rig-02",
            Specs = "RTX 4070 Ti / i7-13700K / 32GB",
            HourlyRate = 6.00m
        },
        new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Rig-03",
            Specs = "RTX 4060 / i5-13400F / 16GB",
            HourlyRate = 4.50m
        },
        new()
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "Console-01",
            Specs = "PlayStation 5 Pro",
            HourlyRate = 5.00m
        },
        new()
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Name = "Rig-04 (maintenance)",
            Specs = "RTX 3080 / i7-12700 / 32GB",
            HourlyRate = 5.50m,
            IsActive = false
        }
    ];
}
