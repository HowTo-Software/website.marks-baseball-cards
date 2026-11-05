using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Entities;
using MarksBaseballCards.Api.Mapping;
using MarksBaseballCards.Api.Services;
using MarksBaseballCards.Shared.Common;
using MarksBaseballCards.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Timeouts;

namespace MarksBaseballCards.Api.Controllers;

[ApiController]
[Route("api/cards")]
[Authorize(Roles = Roles.Admin)]
public class CardsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HistoryWriter _history;
    private readonly PhotoStorage _photos;

    public CardsController(AppDbContext db, HistoryWriter history, PhotoStorage photos)
    {
        _db = db;
        _history = history;
        _photos = photos;
    }

    private string CurrentUser => User.Identity?.Name ?? "unknown";

    [HttpGet]
    [RequestTimeout("reads")]
    public async Task<ActionResult<IEnumerable<CardDto>>> GetAll([FromQuery] string? search, CancellationToken ct)
    {
        var query = _db.Cards.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => EF.Functions.Like(c.PlayerName, $"%{term}%")
                                  || EF.Functions.Like(c.CardNumber, $"%{term}%"));
        }

        // Compatibility endpoint; the staff UI uses /page rather than loading the inventory.
        var cards = await query.OrderBy(c => c.Id).Take(200).Include(c => c.Photos).AsSplitQuery().ToListAsync(ct);
        Response.Headers.Append("Warning", "299 - Use /api/cards/page for paged inventory (this endpoint is limited to 200 records)");
        return Ok(cards.Select(c => c.ToDto()));
    }

    [HttpGet("{id:int}")]
    [RequestTimeout("reads")]
    public async Task<ActionResult<CardDto>> GetById(int id, CancellationToken ct)
    {
        var card = await _db.Cards.AsNoTracking().Include(c => c.Photos).FirstOrDefaultAsync(c => c.Id == id, ct);
        return card is null ? NotFound() : Ok(card.ToDto());
    }

    [HttpGet("page")]
    [RequestTimeout("reads")]
    public async Task<ActionResult<PageResult<CardDto>>> Page([FromQuery] string? search, string status = "all", int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        if (!CardQueries.ValidPage(page, pageSize, search) || status is not ("all" or "listed" or "sold" or "collection" or "draft")) return BadRequest();
        var query = _db.Cards.AsNoTracking().Search(search);
        query = status switch
        {
            "listed" => query.Where(c => c.IsForSale && !c.IsSold),
            "sold" => query.Where(c => c.IsSold),
            "collection" => query.Where(c => c.IsPublished && !c.IsForSale && !c.IsSold),
            "draft" => query.Where(c => !c.IsPublished),
            _ => query
        };
        var total = await query.CountAsync(ct);
        var cards = await query.OrderByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(c => c.Photos).AsSplitQuery().ToListAsync(ct);
        return Ok(new PageResult<CardDto> { Items = cards.Select(c => c.ToDto()).ToList(), TotalCount = total, Page = page, PageSize = pageSize });
    }

    [HttpPost]
    public async Task<ActionResult<CardDto>> Create([FromBody] CardUpsertDto dto, CancellationToken ct)
    {
        if (dto.IsPublished || dto.IsForSale) return BadRequest(new { error = "Save a draft, add photographs, then publish the card." });
        var number = dto.CardNumber.Trim();
        if (await _db.Cards.AnyAsync(c => c.CardNumber == number, ct))
        {
            return Conflict(new { error = $"Card number '{number}' already exists." });
        }

        var card = new Card { CreatedAtUtc = DateTime.UtcNow };
        card.ApplyUpsert(dto);
        card.UpdatedAtUtc = DateTime.UtcNow;

        _db.Cards.Add(card);
        await _history.LogAsync("Created", CurrentUser, $"#{card.CardNumber} {card.PlayerName}", card, false);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsDuplicate(ex)) { return Conflict(new { error = "This card number already exists." }); }

        return CreatedAtAction(nameof(GetById), new { id = card.Id }, card.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CardDto>> Update(int id, [FromBody] CardUpsertDto dto, CancellationToken ct)
    {
        var card = await _db.Cards.Include(c => c.Photos).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (card is null)
        {
            return NotFound();
        }

        if (card.RowVersion is not null && (dto.RowVersion is null || !card.RowVersion.SequenceEqual(dto.RowVersion)))
            return Conflict(new { error = "The card changed. Refresh before saving." });
        if (dto.IsPublished && (card.Photos.Count == 0 || string.IsNullOrWhiteSpace(dto.Description)))
            return BadRequest(new { error = "Add a description and at least one photograph before publishing." });
        if (dto.IsForSale && (!dto.IsPublished || dto.Price is null or <= 0 || card.IsSold))
            return BadRequest(new { error = "Publish the card and set a positive price before listing it for sale." });

        var number = dto.CardNumber.Trim();
        if (!string.Equals(card.CardNumber, number, StringComparison.Ordinal)
            && await _db.Cards.AnyAsync(c => c.CardNumber == number && c.Id != id))
        {
            return Conflict(new { error = $"Card number '{number}' already exists." });
        }

        var wasForSale = card.IsForSale;
        card.ApplyUpsert(dto);
        card.UpdatedAtUtc = DateTime.UtcNow;

        var action = wasForSale switch
        {
            false when card.IsForSale => "Listed",
            true when !card.IsForSale => "Unlisted",
            _ => "Updated"
        };
        await _history.LogAsync(action, CurrentUser, $"#{card.CardNumber} {card.PlayerName}", card, false);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "The card changed. Refresh before saving." }); }
        catch (DbUpdateException ex) when (IsDuplicate(ex)) { return Conflict(new { error = "This card number already exists." }); }

        return Ok(card.ToDto());
    }

    [HttpPost("{id:int}/sell")]
    public async Task<ActionResult<CardDto>> Sell(int id, [FromBody] SellRequest request, CancellationToken ct)
    {
        var card = await _db.Cards.Include(c => c.Photos).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (card is null)
        {
            return NotFound();
        }
        if (card.IsSold)
        {
            return Conflict(new { error = "Card is already marked as sold." });
        }

        card.IsSold = true;
        card.IsForSale = false;
        card.SoldAtUtc = DateTime.UtcNow;
        card.SoldPrice = request.SoldPrice ?? card.Price;
        card.UpdatedAtUtc = DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" ({request.Note.Trim()})";
        await _history.LogAsync("Sold", CurrentUser, $"Manual sale for {card.SoldPrice:C}{note}", card, false);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "The card changed. Refresh before recording the sale." }); }

        return Ok(card.ToDto());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var card = await _db.Cards.Include(c => c.Photos).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (card is null)
        {
            return NotFound();
        }

        // Persist the audit entry first; the FK is configured to null CardId on delete.
        await _history.LogAsync("Deleted", CurrentUser, $"#{card.CardNumber} {card.PlayerName}", card, false);
        var photoIds = card.Photos.Select(p => p.Id).ToArray();
        _db.Cards.Remove(card);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "The card changed. Refresh before deleting." }); }
        foreach (var photoId in photoIds) _photos.Delete(photoId);

        return NoContent();
    }

    private static bool IsDuplicate(DbUpdateException ex) => ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };
}
