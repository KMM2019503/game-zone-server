using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameZoneApi.Data;
using GameZoneApi.Models;

namespace GameZoneApi.Controllers;

/// <summary>
/// Read-only. Machines are fixed reference data seeded by the migration, so this
/// controller exists purely so clients can list the ids they book sessions against.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MachinesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MachinesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MachineResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MachineResponse>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = _db.Machines.AsNoTracking().AsQueryable();

        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        var machines = await query
            .OrderBy(m => m.Name)
            .Select(m => new MachineResponse(m.Id, m.Name, m.Specs, m.HourlyRate, m.IsActive))
            .ToListAsync();

        return Ok(machines);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MachineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MachineResponse>> GetById(Guid id)
    {
        var machine = await _db.Machines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        return machine is null ? NotFound() : Ok(MachineResponse.From(machine));
    }
}
