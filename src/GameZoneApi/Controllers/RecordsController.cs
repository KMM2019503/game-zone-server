using System.Data;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameZoneApi.Data;
using GameZoneApi.Models;

namespace GameZoneApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RecordsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RecordsController(AppDbContext db)
    {
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RecordResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RecordResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? machineId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Records.AsNoTracking().AsQueryable();

        // Non-admins are pinned to their own rows no matter what userId they pass.
        query = IsAdmin
            ? (userId is null ? query : query.Where(r => r.UserId == userId))
            : query.Where(r => r.UserId == CurrentUserId);

        if (machineId is not null)
            query = query.Where(r => r.MachineId == machineId);
        if (from is not null)
            query = query.Where(r => r.StartedAt >= from);
        if (to is not null)
            query = query.Where(r => r.StartedAt < to);

        var total = await query.CountAsync();
        var records = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RecordResponse(
                r.Id, r.UserId, r.User.FullName, r.MachineId, r.Machine.Name,
                r.StartedAt, r.StartedAt.AddMinutes(r.DurationMinutes), r.DurationMinutes,
                r.Cost, r.Notes, r.CreatedAt))
            .ToListAsync();

        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(records);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecordResponse>> GetById(Guid id)
    {
        var record = await _db.Records
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Machine)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record is null) return NotFound();
        if (record.UserId != CurrentUserId && !IsAdmin) return Forbid();

        return Ok(RecordResponse.From(record));
    }

    [HttpPost]
    [ProducesResponseType(typeof(RecordResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordResponse>> Create(CreateRecordRequest request)
    {
        var ownerId = IsAdmin && request.UserId is not null ? request.UserId.Value : CurrentUserId;

        if (ownerId != CurrentUserId && !await _db.Users.AnyAsync(u => u.Id == ownerId))
            return BadRequest(new { message = "Unknown user." });

        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == request.MachineId);
        if (machine is null)
            return BadRequest(new { message = "Unknown machine." });
        if (!machine.IsActive)
            return BadRequest(new { message = $"Machine '{machine.Name}' is not available." });

        var startedAt = Normalize(request.StartedAt);
        if (await OverlapsAsync(machine.Id, startedAt, request.DurationMinutes))
            return Conflict(new { message = $"Machine '{machine.Name}' is already booked for that time." });

        var record = new GameRecord
        {
            UserId = ownerId,
            MachineId = machine.Id,
            StartedAt = startedAt,
            DurationMinutes = request.DurationMinutes,
            Cost = GameRecord.CalculateCost(machine.HourlyRate, request.DurationMinutes),
            Notes = request.Notes?.Trim()
        };

        _db.Records.Add(record);
        await _db.SaveChangesAsync();

        // Add() only knows the foreign keys; load the navigations so the response
        // can carry the user and machine names.
        await _db.Entry(record).Reference(r => r.User).LoadAsync();
        await _db.Entry(record).Reference(r => r.Machine).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = record.Id }, RecordResponse.From(record));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecordResponse>> Update(Guid id, UpdateRecordRequest request)
    {
        var record = await _db.Records.FirstOrDefaultAsync(r => r.Id == id);
        if (record is null) return NotFound();
        if (record.UserId != CurrentUserId && !IsAdmin) return Forbid();

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var startedAt = Normalize(request.StartedAt);
        var hasPaymentHistory = await _db.Payments
            .AnyAsync(p => p.RecordId == record.Id);

        if (hasPaymentHistory)
        {
            var billingDetailsChanged =
                request.MachineId != record.MachineId ||
                startedAt != record.StartedAt ||
                request.DurationMinutes != record.DurationMinutes;

            if (billingDetailsChanged)
            {
                return Conflict(new
                {
                    message = "Billing details cannot be changed after payment activity has been recorded."
                });
            }

            // Notes do not affect the charge. Do not recalculate Cost here because it
            // is the rate snapshot that the existing payment history was based on.
            record.Notes = request.Notes?.Trim();
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            await _db.Entry(record).Reference(r => r.User).LoadAsync();
            await _db.Entry(record).Reference(r => r.Machine).LoadAsync();

            return Ok(RecordResponse.From(record));
        }

        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == request.MachineId);
        if (machine is null)
            return BadRequest(new { message = "Unknown machine." });
        if (!machine.IsActive && machine.Id != record.MachineId)
            return BadRequest(new { message = $"Machine '{machine.Name}' is not available." });

        if (await OverlapsAsync(machine.Id, startedAt, request.DurationMinutes, excludeId: record.Id))
            return Conflict(new { message = $"Machine '{machine.Name}' is already booked for that time." });

        record.MachineId = machine.Id;
        record.StartedAt = startedAt;
        record.DurationMinutes = request.DurationMinutes;
        record.Cost = GameRecord.CalculateCost(machine.HourlyRate, request.DurationMinutes);
        record.Notes = request.Notes?.Trim();

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        await _db.Entry(record).Reference(r => r.User).LoadAsync();
        await _db.Entry(record).Reference(r => r.Machine).LoadAsync();

        return Ok(RecordResponse.From(record));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var record = await _db.Records.FirstOrDefaultAsync(r => r.Id == id);
        if (record is null) return NotFound();
        if (record.UserId != CurrentUserId && !IsAdmin) return Forbid();

        if (await _db.Payments.AnyAsync(p => p.RecordId == record.Id))
        {
            return Conflict(new
            {
                message = "A record with payment history cannot be deleted."
            });
        }

        _db.Records.Remove(record);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsPaymentReferenceViolation(exception))
        {
            // A payment may have been created after AnyAsync but before this delete.
            // The restricted database foreign key is the final concurrency safeguard.
            return Conflict(new
            {
                message = "A record with payment history cannot be deleted."
            });
        }

        return NoContent();
    }

    private static bool IsPaymentReferenceViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 547 } sqlException &&
        sqlException.Message.Contains(
            "FK_Payments_Records_RecordId",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A client may send "2026-08-10T14:00:00" with no offset; treat that as UTC so it
    /// never lands in the database as a local time.
    /// </summary>
    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    /// <summary>
    /// One machine can only be played by one person at a time. Two ranges overlap when
    /// each starts before the other ends.
    /// </summary>
    private Task<bool> OverlapsAsync(Guid machineId, DateTime startedAt, int durationMinutes, Guid? excludeId = null)
    {
        var endsAt = startedAt.AddMinutes(durationMinutes);

        return _db.Records.AnyAsync(r =>
            r.MachineId == machineId &&
            (excludeId == null || r.Id != excludeId) &&
            r.StartedAt < endsAt &&
            startedAt < r.StartedAt.AddMinutes(r.DurationMinutes));
    }
}
