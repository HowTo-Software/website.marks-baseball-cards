namespace MarksBaseballCards.Shared.Models;

/// <summary>Full card representation returned to authenticated admin users.</summary>
public class CardDto
{
    public string? Description { get; set; }
    public bool IsPublished { get; set; }
    public byte[]? RowVersion { get; set; }
    public int PhotoCount { get; set; }
    public List<CardPhotoDto> Photos { get; set; } = [];
    public int Id { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;

    public bool InPlastic { get; set; }
    public bool InSet1 { get; set; }
    public bool InSet2 { get; set; }
    public bool InSet3 { get; set; }
    public bool InSet4 { get; set; }
    public bool InSet5 { get; set; }
    public bool InSet6 { get; set; }

    public int Doubles { get; set; }
    public bool IsRookie { get; set; }
    public bool IsRoyals { get; set; }

    // Marketplace
    public bool IsForSale { get; set; }
    public decimal? Price { get; set; }
    public string? Condition { get; set; }
    public string? ListingNotes { get; set; }
    public bool IsSold { get; set; }
    public DateTime? SoldAtUtc { get; set; }
    public decimal? SoldPrice { get; set; }

    /// <summary>Number of the six master sets this card is currently filed in.</summary>
    public int SetsFiledCount =>
        (InSet1 ? 1 : 0) + (InSet2 ? 1 : 0) + (InSet3 ? 1 : 0) +
        (InSet4 ? 1 : 0) + (InSet5 ? 1 : 0) + (InSet6 ? 1 : 0);
}
