using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GameZoneApi.Models;

namespace GameZoneApi.Data;

public static class DbInitializer
{
    /// <summary>
    /// Applies pending migrations and seeds a development admin account.
    /// Development only - production should run "dotnet ef database update" as a deploy step.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await db.Database.MigrateAsync();

        const string adminEmail = "admin@gamezone.local";
        if (await db.Users.AnyAsync(u => u.Email == adminEmail))
            return;

        var admin = new User
        {
            Email = adminEmail,
            FullName = "Local Admin",
            Role = Roles.Admin
        };
        admin.PasswordHash = hasher.HashPassword(admin, "Admin123!");

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
