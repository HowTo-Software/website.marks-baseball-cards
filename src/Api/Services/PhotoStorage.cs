using SkiaSharp;
using MarksBaseballCards.Shared.Common;

namespace MarksBaseballCards.Api.Services;

public sealed record ProcessedPhoto(byte[] Image, byte[] Thumbnail, int Width, int Height);
public sealed class PhotoValidationException(string message) : Exception(message);
public sealed class PhotoBusyException : Exception { }

/// <summary>Decode and re-encode pixels, never publish an untrusted original or its metadata.</summary>
public sealed class PhotoStorage
{
    public const long MaxUploadBytes = CardPhotoLimits.MaxFileBytes;
    public const int MaxPhotos = CardPhotoLimits.MaxPhotos;
    private readonly string _root;
    private readonly ILogger<PhotoStorage> _logger;
    private readonly SemaphoreSlim _processing = new(2, 2);
    public PhotoStorage(IConfiguration config, IWebHostEnvironment env, ILogger<PhotoStorage> logger)
    {
        _logger = logger;
        _root = Path.GetFullPath(config["Photos:RootPath"] ?? Path.Combine(env.ContentRootPath, "App_Data", "card-photos"));
        var webRoot = Path.GetFullPath(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
        if (_root.Equals(webRoot, StringComparison.OrdinalIgnoreCase) || _root.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Photo storage must be outside the public web root.");
        Directory.CreateDirectory(_root);
    }

    public async Task<ProcessedPhoto> ProcessAsync(Stream input, CancellationToken ct)
    {
        if (!await _processing.WaitAsync(0, ct)) throw new PhotoBusyException();
        try
        {
            using var buffer = new MemoryStream();
            var block = new byte[65536];
            int read;
            while ((read = await input.ReadAsync(block, ct)) > 0)
            {
                if (buffer.Length + read > MaxUploadBytes) throw new PhotoValidationException("Photos must be 30 MB or smaller.");
                buffer.Write(block, 0, read);
            }
            if (buffer.Length == 0) throw new PhotoValidationException("Choose a nonempty photo.");
            using var data = SKData.CreateCopy(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
                throw new PhotoValidationException("Choose a valid JPEG, PNG or WebP photo.");
            var info = codec.Info;
            if (info.Width < 1 || info.Height < 1 || info.Width > 10000 || info.Height > 10000 || (long)info.Width * info.Height > 16_000_000 || codec.FrameCount > 1)
                throw new PhotoValidationException("Use a still photo of at most 16 megapixels.");
            using var decoded = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
                throw new PhotoValidationException("This photo is damaged or incomplete.");
            ct.ThrowIfCancellationRequested();
            var swapped = (int)codec.EncodedOrigin >= 5;
            using var oriented = new SKBitmap(swapped ? info.Height : info.Width, swapped ? info.Width : info.Height);
            using (var canvas = new SKCanvas(oriented))
            {
                canvas.Clear(SKColors.White);
                var w = info.Width; var h = info.Height;
                var m = codec.EncodedOrigin switch
                {
                    SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
                    SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
                    SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
                    SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
                    SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
                    SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
                    SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
                    _ => SKMatrix.Identity
                };
                canvas.SetMatrix(m);
                canvas.DrawBitmap(decoded, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
            }
            var full = Encode(oriented, 1600, 85);
            var thumb = Encode(oriented, 360, 78);
            ct.ThrowIfCancellationRequested();
            var scale = Math.Min(1d, 1600d / Math.Max(oriented.Width, oriented.Height));
            return new(full, thumb, Math.Max(1, (int)Math.Round(oriented.Width * scale)), Math.Max(1, (int)Math.Round(oriented.Height * scale)));
        }
        finally { _processing.Release(); }
    }

    private static byte[] Encode(SKBitmap source, int limit, int quality)
    {
        var scale = Math.Min(1d, limit / (double)Math.Max(source.Width, source.Height));
        var info = new SKImageInfo(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)));
        using var resized = source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear))
            ?? throw new PhotoValidationException("The photo could not be resized.");
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality)
            ?? throw new PhotoValidationException("The photo could not be encoded.");
        return encoded.ToArray();
    }

    public string PathFor(Guid id, bool thumbnail) => Path.Combine(_root, $"{id:N}{(thumbnail ? "-thumb" : "")}.jpg");
    public async Task WriteAsync(Guid id, ProcessedPhoto photo, CancellationToken ct)
    {
        try
        {
            await using (var file = new FileStream(PathFor(id, false), FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                await file.WriteAsync(photo.Image, ct);
            await using (var file = new FileStream(PathFor(id, true), FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                await file.WriteAsync(photo.Thumbnail, ct);
        }
        catch { Delete(id); throw; }
    }
    public void Delete(Guid id)
    {
        foreach (var thumbnail in new[] { false, true })
            try { File.Delete(PathFor(id, thumbnail)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _logger.LogWarning("Photograph {PhotoId} requires storage cleanup; metadata removal is preserved.", id); }
    }
}
