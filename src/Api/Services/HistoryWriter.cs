using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Entities;

namespace MarksBaseballCards.Api.Services;

/// <summary>Writes immutable audit-trail entries used by the system-admin dashboard.</summary>
public class HistoryWriter
{
    private readonly AppDbContext _db;

    public HistoryWriter(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string action, string changedBy, string? details = null, Card? card = null, bool save = true)
    {
        _db.History.Add(new CardHistory
        {
            CardId = card?.Id,
            Card = card,
            CardNumber = card?.CardNumber ?? string.Empty,
            PlayerName = card?.PlayerName ?? string.Empty,
            Action = action,
            Details = Truncate(details, 1000),
            ChangedBy = changedBy,
            ChangedAtUtc = DateTime.UtcNow
        });

        if (save)
        {
            await _db.SaveChangesAsync();
        }
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
