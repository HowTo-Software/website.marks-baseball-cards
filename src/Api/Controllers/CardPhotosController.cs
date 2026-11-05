using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Entities;
using MarksBaseballCards.Api.Mapping;
using MarksBaseballCards.Api.Services;
using MarksBaseballCards.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace MarksBaseballCards.Api.Controllers;

[ApiController]
public sealed class CardPhotosController(AppDbContext db, PhotoStorage storage, HistoryWriter history) : ControllerBase
{
    [HttpPost("api/cards/{cardId:int}/photos")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting("photos")]
    [RequestSizeLimit(CardPhotoLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = CardPhotoLimits.MaxRequestBytes)]
    public async Task<IActionResult> Upload(int cardId, [FromForm] IFormFile file, [FromForm] string? caption, CancellationToken ct)
    {
        if (file.Length is < 1 or > PhotoStorage.MaxUploadBytes || (caption?.Length ?? 0) > 200)
            return BadRequest(new { error = "Use a photo up to 30 MB and a caption up to 200 characters." });
        var card = await db.Cards.Include(c => c.Photos).FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null) return NotFound();
        if (card.Photos.Count >= PhotoStorage.MaxPhotos) return Conflict(new { error = "A card can have up to six photos." });
        var id = Guid.NewGuid();
        try
        {
            await using var input = file.OpenReadStream();
            var processed = await storage.ProcessAsync(input, ct);
            await storage.WriteAsync(id, processed, ct);
            var photo = new CardPhoto { Id = id, CardId = cardId, Caption = caption?.Trim() ?? "", Position = card.Photos.Select(p => p.Position).DefaultIfEmpty(-1).Max() + 1,
                Width = processed.Width, Height = processed.Height, Bytes = processed.Image.LongLength + processed.Thumbnail.LongLength, CreatedAtUtc = DateTime.UtcNow };
            db.CardPhotos.Add(photo);
            card.UpdatedAtUtc = DateTime.UtcNow; // rowversion serializes concurrent quota changes.
            await history.LogAsync("PhotoAdded", User.Identity?.Name ?? "unknown", "Card photograph added.", card, save: false);
            await db.SaveChangesAsync(ct);
            return Created($"/api/card-photos/{id}/image", photo.ToDto());
        }
        catch (PhotoBusyException) { return StatusCode(429, new { error = "Photo processing is busy. Please try again shortly." }); }
        catch (PhotoValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DbUpdateConcurrencyException) { storage.Delete(id); return Conflict(new { error = "The card changed. Refresh before uploading again." }); }
        // A general DB/network failure may have an ambiguous commit result. Keep the inert
        // files rather than delete a possibly committed photograph; reconcile orphans offline.
    }

    [HttpDelete("api/cards/{cardId:int}/photos/{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int cardId, Guid id, CancellationToken ct)
    {
        var photo = await db.CardPhotos.Include(p => p.Card).FirstOrDefaultAsync(p => p.Id == id && p.CardId == cardId, ct);
        if (photo is null) return NotFound();
        // Never leave a published card without a photograph.
        if (photo.Card.IsPublished && !await db.CardPhotos.AnyAsync(p => p.CardId == cardId && p.Id != id, ct))
            return Conflict(new { error = "Unpublish the card before removing its last photo." });
        photo.Card.UpdatedAtUtc = DateTime.UtcNow;
        db.CardPhotos.Remove(photo);
        await history.LogAsync("PhotoRemoved", User.Identity?.Name ?? "unknown", "Card photograph removed.", photo.Card, false);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "The card changed. Refresh and try again." }); }
        storage.Delete(id);
        return NoContent();
    }

    [HttpPut("api/cards/{cardId:int}/photos/{id:guid}/cover")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Cover(int cardId, Guid id, CancellationToken ct)
    {
        var card = await db.Cards.Include(c => c.Photos).FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null || card.Photos.All(p => p.Id != id)) return NotFound();
        var photos = card.Photos.OrderBy(p => p.Position).ThenBy(p => p.Id).ToList();
        photos = photos.OrderByDescending(p => p.Id == id).ToList();
        for (var i = 0; i < photos.Count; i++) photos[i].Position = i;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await history.LogAsync("PhotoCover", User.Identity?.Name ?? "unknown", "Cover photograph changed.", card, false);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "The card changed. Refresh and try again." }); }
        return NoContent();
    }

    [HttpGet("api/card-photos/{id:guid}/{variant}")]
    [AllowAnonymous]
    public async Task<IActionResult> Image(Guid id, string variant, CancellationToken ct)
    {
        if (variant is not ("image" or "thumbnail")) return NotFound();
        var photo = await db.CardPhotos.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.Card.IsPublished }).FirstOrDefaultAsync(ct);
        if (photo is null || (!photo.IsPublished && !User.IsInRole(Roles.Admin))) return NotFound();
        var path = storage.PathFor(id, variant == "thumbnail");
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl = photo.IsPublished ? "private, max-age=300" : "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return PhysicalFile(path, "image/jpeg", enableRangeProcessing: true);
    }
}
