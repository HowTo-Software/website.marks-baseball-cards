namespace MarksBaseballCards.Api.Entities;

public sealed class CardPhoto
{
    public Guid Id { get; set; }
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public string Caption { get; set; } = "";
    public int Position { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Bytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
