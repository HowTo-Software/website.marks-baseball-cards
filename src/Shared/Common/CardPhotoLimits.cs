namespace MarksBaseballCards.Shared.Common;

public static class CardPhotoLimits
{
    public const long MaxFileBytes = 30L * 1024 * 1024;
    public const int MaxPhotos = 6;
    public const long MaxRequestBytes = MaxFileBytes + 65536; // Multipart headers and caption.
}
