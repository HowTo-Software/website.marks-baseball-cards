namespace MarksBaseballCards.Api.Entities;

/// <summary>A single baseball card in the collection / inventory.</summary>
public class Card
{
    public string? Description { get; set; }
    public bool IsPublished { get; set; }
    public List<CardPhoto> Photos { get; set; } = [];
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

    // Marketplace / Stripe
    public bool IsForSale { get; set; }
    public decimal? Price { get; set; }
    public string? Condition { get; set; }
    public string? ListingNotes { get; set; }
    public bool IsSold { get; set; }
    public DateTime? SoldAtUtc { get; set; }
    public decimal? SoldPrice { get; set; }

    /// <summary>Most recent Stripe Checkout Session id created for this card (idempotency aid).</summary>
    public string? StripeCheckoutSessionId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>SQL Server rowversion used for optimistic concurrency on edits.</summary>
    public byte[]? RowVersion { get; set; }
}
