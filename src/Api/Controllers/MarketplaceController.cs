using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Mapping;
using MarksBaseballCards.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MarksBaseballCards.Api.Services;
using Microsoft.AspNetCore.Http.Timeouts;

namespace MarksBaseballCards.Api.Controllers;

[ApiController]
[Route("api/marketplace")]
[AllowAnonymous]
public class MarketplaceController : ControllerBase
{
    private readonly AppDbContext _db;

    public MarketplaceController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Public listing of cards currently for sale (optionally including recently sold).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MarketplaceCardDto>>> Get(
        [FromQuery] string? search, [FromQuery] bool includeSold = false)
    {
        var query = _db.Cards.AsNoTracking().Where(c => c.IsPublished && (c.IsForSale || (includeSold && c.IsSold)));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => EF.Functions.Like(c.PlayerName, $"%{term}%")
                                  || EF.Functions.Like(c.CardNumber, $"%{term}%"));
        }

        var cards = await query
            .OrderBy(c => c.IsSold)
            .ThenBy(c => c.PlayerName)
            .Take(200).Include(c => c.Photos).AsSplitQuery().ToListAsync(HttpContext.RequestAborted);

        return Ok(cards.Select(c => c.ToMarketplaceDto()));
    }

    [HttpGet("page")]
    [RequestTimeout("reads")]
    public async Task<IActionResult> Page(string? search, bool includeSold = false, string category = "all", string sort = "newest", int page = 1, int pageSize = 24, CancellationToken ct = default)
    {
        if (!CardQueries.ValidPage(page, pageSize, search) || category is not ("all" or "rookie" or "royals") || sort is not ("newest" or "name" or "price-low" or "price-high")) return BadRequest();
        return Ok(await CardQueries.PublicPage(_db, search, true, includeSold, category, sort, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MarketplaceCardDto>> GetById(int id)
    {
        var card = await _db.Cards.AsNoTracking().Include(c => c.Photos)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsPublished && (c.IsForSale || c.IsSold), HttpContext.RequestAborted);
        return card is null ? NotFound() : Ok(card.ToMarketplaceDto());
    }
}
