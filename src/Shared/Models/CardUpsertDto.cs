using System.ComponentModel.DataAnnotations;

namespace MarksBaseballCards.Shared.Models;

/// <summary>Editable card payload used by admins to create or update a card.</summary>
public class CardUpsertDto : IValidatableObject
{
    [StringLength(2000)]
    public string? Description { get; set; }
    public bool IsPublished { get; set; }
    [MaxLength(8)] public byte[]? RowVersion { get; set; }
    [Required(ErrorMessage = "Card number is required.")]
    [StringLength(20, ErrorMessage = "Card number must be 20 characters or fewer.")]
    public string CardNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Player name is required.")]
    [StringLength(120, ErrorMessage = "Player name must be 120 characters or fewer.")]
    public string PlayerName { get; set; } = string.Empty;

    public bool InPlastic { get; set; }
    public bool InSet1 { get; set; }
    public bool InSet2 { get; set; }
    public bool InSet3 { get; set; }
    public bool InSet4 { get; set; }
    public bool InSet5 { get; set; }
    public bool InSet6 { get; set; }

    [Range(0, 1000, ErrorMessage = "Doubles must be between 0 and 1000.")]
    public int Doubles { get; set; }

    public bool IsRookie { get; set; }
    public bool IsRoyals { get; set; }

    public bool IsForSale { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Price must be between 0 and 1,000,000.")]
    public decimal? Price { get; set; }

    [StringLength(60, ErrorMessage = "Condition must be 60 characters or fewer.")]
    public string? Condition { get; set; }

    [StringLength(500, ErrorMessage = "Listing notes must be 500 characters or fewer.")]
    public string? ListingNotes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Price is { } price && decimal.Round(price, 2) != price)
            yield return new("Use a price with at most two decimal places.", [nameof(Price)]);
        if (IsPublished && string.IsNullOrWhiteSpace(Description))
            yield return new("Add a description before publishing.", [nameof(Description)]);
        if (IsForSale && (!IsPublished || Price is null or <= 0))
            yield return new("Publish the card and set a positive price before listing it for sale.", [nameof(Price)]);
    }
}
