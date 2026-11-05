using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Entities;
using MarksBaseballCards.Api.Mapping;
using MarksBaseballCards.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MarksBaseballCards.Api.Services;

public static class CardQueries
{
    public static IQueryable<Card> Search(this IQueryable<Card> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim().TrimStart('#');
        return query.Where(c => c.CardNumber.StartsWith(term) || c.PlayerName.Contains(term));
    }

    public static async Task<PageResult<MarketplaceCardDto>> PublicPage(AppDbContext db, string? search,
        bool market, bool includeSold, string category, string sort, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Cards.AsNoTracking().Where(c => c.IsPublished);
        if (market) query = query.Where(c => (c.IsForSale && !c.IsSold) || (includeSold && c.IsSold));
        query = query.Search(search);
        if (category == "rookie") query = query.Where(c => c.IsRookie);
        if (category == "royals") query = query.Where(c => c.IsRoyals);
        var total = await query.CountAsync(ct);
        var ordered = sort switch
        {
            "name" => query.OrderBy(c => c.PlayerName).ThenBy(c => c.Id),
            "price-low" => query.OrderBy(c => c.Price).ThenBy(c => c.Id),
            "price-high" => query.OrderByDescending(c => c.Price).ThenBy(c => c.Id),
            _ => query.OrderByDescending(c => c.Id)
        };
        // Only scalar listing fields and the cover ID: never image bytes, staff data or per-row queries.
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new MarketplaceCardDto
            {
                Id = c.Id, CardNumber = c.CardNumber, PlayerName = c.PlayerName, Description = c.Description,
                IsForSale = c.IsForSale, IsSold = c.IsSold, Price = c.Price, Condition = c.Condition,
                ListingNotes = c.ListingNotes, IsRookie = c.IsRookie, IsRoyals = c.IsRoyals,
                CoverPhotoId = c.Photos.OrderBy(p => p.Position).ThenBy(p => p.Id).Select(p => (Guid?)p.Id).FirstOrDefault()
            }).ToListAsync(ct);
        return new() { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public static bool ValidPage(int page, int pageSize, string? search) =>
        page is >= 1 and <= 1000 && pageSize is >= 1 and <= 50 && (search?.Length ?? 0) <= 100;
}
