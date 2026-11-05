using System.Text;
using MarksBaseballCards.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace MarksBaseballCards.Tests;

public sealed class PhotoProcessingTests
{
    [Fact]
    public async Task ProducesBoundedJpegAndThumbnailFromPixels()
    {
        using var host = new TestHost();
        var storage = host.Services.GetRequiredService<PhotoStorage>();
        await using var stream = new MemoryStream(PhotoWorkflowTests.Picture(2400, 1200));
        var photo = await storage.ProcessAsync(stream, default);
        Assert.Equal(1600, photo.Width); Assert.Equal(800, photo.Height);
        using var full = SKBitmap.Decode(photo.Image); using var thumbnail = SKBitmap.Decode(photo.Thumbnail);
        Assert.Equal(1600, full.Width); Assert.Equal(360, thumbnail.Width);
        Assert.Equal(0xff, photo.Image[0]); Assert.Equal(0xd8, photo.Image[1]);
        Assert.DoesNotContain("Exif", Encoding.Latin1.GetString(photo.Image));
    }

    [Fact]
    public async Task CorrectsExifOrientationAndDiscardsTheOriginalMetadata()
    {
        using var host = new TestHost(); var storage = host.Services.GetRequiredService<PhotoStorage>();
        using var bitmap = new SKBitmap(120, 180); bitmap.Erase(SKColors.DarkBlue);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        byte[] exif = [255,225,0,34,69,120,105,102,0,0,77,77,0,42,0,0,0,8,0,1,1,18,0,3,0,0,0,1,0,6,0,0,0,0,0,0];
        var original = data.ToArray();
        await using var input = new MemoryStream(original[..2].Concat(exif).Concat(original[2..]).ToArray());
        var photo = await storage.ProcessAsync(input, default);
        Assert.Equal(180, photo.Width); Assert.Equal(120, photo.Height);
        Assert.DoesNotContain("Exif", Encoding.Latin1.GetString(photo.Image));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<svg onload='alert(1)'></svg>")]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("GIF89a")]
    public async Task RejectsNonPhotographicPayloads(string payload)
    {
        using var host = new TestHost(); var storage = host.Services.GetRequiredService<PhotoStorage>();
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        await Assert.ThrowsAsync<PhotoValidationException>(() => storage.ProcessAsync(input, default));
    }

    [Fact]
    public async Task BoundsActualStreamLengthRatherThanTrustingBrowserMetadata()
    {
        using var host = new TestHost(); var storage = host.Services.GetRequiredService<PhotoStorage>();
        await using var input = new MemoryStream(new byte[PhotoStorage.MaxUploadBytes + 1]);
        await Assert.ThrowsAsync<PhotoValidationException>(() => storage.ProcessAsync(input, default));
    }
}
