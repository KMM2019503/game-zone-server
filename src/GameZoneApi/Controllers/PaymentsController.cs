using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameZoneApi.Data;
using GameZoneApi.Models;

namespace GameZoneApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaymentsController(AppDbContext db)
    {
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PaymentResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? recordId = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Payments.AsNoTracking().AsQueryable();

        // A non-admin is always limited through Payment -> Record -> UserId. Passing
        // another user's recordId cannot bypass this ownership filter.
        if (!IsAdmin)
            query = query.Where(p => p.Record.UserId == CurrentUserId);

        if (recordId is not null)
            query = query.Where(p => p.RecordId == recordId);

        var total = await query.CountAsync();
        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentResponse(
                p.Id,
                p.RecordId,
                p.Amount,
                p.Type,
                p.Method,
                p.Status,
                p.TransactionReference,
                p.CompletedAt,
                p.Notes,
                p.CreatedAt))
            .ToListAsync();

        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(payments);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetById(Guid id)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Record)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
            return NotFound();

        if (!IsAdmin && payment.Record.UserId != CurrentUserId)
            return Forbid();

        return Ok(PaymentResponse.From(payment));
    }

    /// <summary>
    /// Records a payment confirmed by an administrator or cashier. This endpoint does
    /// not contact a card or mobile-wallet provider; those integrations should create
    /// pending payments and complete them from a trusted provider callback.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Create(CreatePaymentRequest request)
    {
        if (request.RecordId == Guid.Empty)
            return BadRequest(new { message = "RecordId must not be empty." });

        if (decimal.Round(request.Amount, 2) != request.Amount)
            return BadRequest(new { message = "Amount can have at most two decimal places." });

        if (request.Method != PaymentMethods.Cash &&
            string.IsNullOrWhiteSpace(request.TransactionReference))
        {
            return BadRequest(new
            {
                message = "TransactionReference is required for card and mobile-wallet payments."
            });
        }

        // Serializable isolation prevents two simultaneous partial payments from both
        // reading the same balance and together charging more than the record's cost.
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var record = await _db.Records
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RecordId);

        if (record is null)
            return NotFound(new { message = "Record was not found." });

        var netPaid = await _db.Payments
            .Where(p => p.RecordId == record.Id &&
                        p.Status == PaymentStatuses.Completed)
            .SumAsync(p => p.Type == PaymentTypes.Refund ? -p.Amount : p.Amount);

        var balanceDue = record.Cost - netPaid;
        if (balanceDue <= 0)
            return Conflict(new { message = "This record is already fully paid." });

        if (request.Amount > balanceDue)
        {
            return Conflict(new
            {
                message = "Payment exceeds the outstanding balance.",
                balanceDue
            });
        }

        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            RecordId = record.Id,
            Amount = request.Amount,
            Type = PaymentTypes.Payment,
            Method = request.Method,
            Status = PaymentStatuses.Completed,
            TransactionReference = NormalizeOptional(request.TransactionReference),
            CompletedAt = now,
            Notes = NormalizeOptional(request.Notes),
            CreatedAt = now
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = payment.Id },
            PaymentResponse.From(payment));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
