using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using GameZoneApi.Models;

namespace GameZoneApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<GameRecord> Records => Set<GameRecord>();
    public DbSet<Payment> Payments => Set<Payment>();

    // datetime2 carries no DateTimeKind, so values read back from SQL Server come out
    // Unspecified and serialize without the trailing 'Z'. Tag them as UTC on the way out.
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
        new(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
            entity.Property(u => u.Role).IsRequired().HasMaxLength(32);
            entity.Property(u => u.CreatedAt).HasConversion(UtcConverter);
            entity.Property(u => u.LastLoginAt).HasConversion(NullableUtcConverter);
        });

        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(m => m.Name).IsUnique();
            entity.Property(m => m.Specs).IsRequired().HasMaxLength(256);
            entity.Property(m => m.HourlyRate).HasColumnType("decimal(10,2)");

            // Fixed reference data: the rows ship inside the migration, so there is no
            // create/update/delete API for machines.
            entity.HasData(Machine.Seed);
        });

        modelBuilder.Entity<GameRecord>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Cost).HasColumnType("decimal(10,2)");
            entity.Property(r => r.Notes).HasMaxLength(500);
            entity.Property(r => r.StartedAt).HasConversion(UtcConverter);
            entity.Property(r => r.CreatedAt).HasConversion(UtcConverter);

            // EndedAt is derived from StartedAt + DurationMinutes, not stored.
            entity.Ignore(r => r.EndedAt);

            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not Cascade: a machine must not be removable while sessions
            // reference it, or the billing history disappears with it.
            entity.HasOne(r => r.Machine)
                .WithMany(m => m.Records)
                .HasForeignKey(r => r.MachineId)
                .OnDelete(DeleteBehavior.Restrict);

            // The common query is "this user's sessions, newest first".
            entity.HasIndex(r => new { r.UserId, r.StartedAt });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasColumnType("decimal(10,2)");
            entity.Property(p => p.Type).IsRequired().HasMaxLength(16);
            entity.Property(p => p.Method).IsRequired().HasMaxLength(32);
            entity.Property(p => p.Status).IsRequired().HasMaxLength(32);
            entity.Property(p => p.TransactionReference).HasMaxLength(100);
            entity.Property(p => p.Notes).HasMaxLength(500);
            entity.Property(p => p.CompletedAt).HasConversion(NullableUtcConverter);
            entity.Property(p => p.CreatedAt).HasConversion(UtcConverter);

            // Keep financial history when a play-session record exists. The API must
            // reject deleting a record that has payments instead of cascading the delete.
            entity.HasOne(p => p.Record)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.RecordId)
                .OnDelete(DeleteBehavior.Restrict);

            // The common query is "payments for this record, newest first".
            entity.HasIndex(p => new { p.RecordId, p.CreatedAt });

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount_Positive", "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_Payments_Type_Valid",
                    "[Type] IN ('Payment', 'Refund')");
                table.HasCheckConstraint(
                    "CK_Payments_Method_Valid",
                    "[Method] IN ('Cash', 'Card', 'MobileWallet')");
                table.HasCheckConstraint(
                    "CK_Payments_Status_Valid",
                    "[Status] IN ('Pending', 'Completed', 'Failed')");
            });
        });
    }
}
