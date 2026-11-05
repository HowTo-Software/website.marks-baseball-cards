namespace MarksBaseballCards.Shared.Models;

public sealed class CardPhotoDto
{
    public Guid Id { get; set; }
    public string Caption { get; set; } = "";
    public string Url => $"api/card-photos/{Id}/image";
    public string ThumbnailUrl => $"api/card-photos/{Id}/thumbnail";
    public int Position { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class PageResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
