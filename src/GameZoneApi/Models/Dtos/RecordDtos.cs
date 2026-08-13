using System.ComponentModel.DataAnnotations;

namespace GameZoneApi.Models;

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
