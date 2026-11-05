namespace MarksBaseballCards.Shared.Models;

/// <summary>Public, read-only marketplace listing. Exposes only buyer-relevant fields.</summary>
public class MarketplaceCardDto
{
    public string? Description { get; set; }
    public Guid? CoverPhotoId { get; set; }
    public List<CardPhotoDto> Photos { get; set; } = [];
    public bool IsForSale { get; set; }
    public int Id { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public bool IsRookie { get; set; }
    public bool IsRoyals { get; set; }
    public decimal? Price { get; set; }
    public string? Condition { get; set; }
    public string? ListingNotes { get; set; }
    public bool IsSold { get; set; }
}
