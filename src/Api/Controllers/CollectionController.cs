using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Mapping;
using MarksBaseballCards.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarksBaseballCards.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/collection")]
public sealed class CollectionController(AppDbContext db) : ControllerBase
{
    [HttpGet, RequestTimeout("reads")]
    public async Task<IActionResult> Page(string? search, string category = "all", string sort = "newest", int page = 1, int pageSize = 24, CancellationToken ct = default)
    {
        if (!CardQueries.ValidPage(page, pageSize, search) || category is not ("all" or "rookie" or "royals") || sort is not ("newest" or "name" or "price-low" or "price-high")) return BadRequest();
        return Ok(await CardQueries.PublicPage(db, search, false, false, category, sort, page, pageSize, ct));
    }
    [HttpGet("{id:int}"), RequestTimeout("reads")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var card = await db.Cards.AsNoTracking().Include(c => c.Photos).FirstOrDefaultAsync(c => c.Id == id && c.IsPublished, ct);
        return card is null ? NotFound() : Ok(card.ToMarketplaceDto());
    }
}
