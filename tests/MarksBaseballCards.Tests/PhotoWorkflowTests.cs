using System.Net;
using System.Net.Http.Json;
using MarksBaseballCards.Shared.Models;
using SkiaSharp;
using Xunit;

namespace MarksBaseballCards.Tests;

public sealed class PhotoWorkflowTests
{
    public static byte[] Picture(int width = 120, int height = 180)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.DarkRed);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
    private static MultipartFormDataContent Form(byte[] image, string caption = "Front")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(image), "file", "../../unsafe.svg");
        form.Add(new StringContent(caption), "caption");
        return form;
    }

    [Fact]
    public async Task DraftPhotosArePrivateAndPublicationIsSeparateFromSelling()
    {
        using var host = new TestHost(); await host.InitializeAsync();
        using var owner = host.Client("Admin"); using var visitor = host.Client();
        var created = await owner.PostAsJsonAsync("/api/cards", new CardUpsertDto { CardNumber = "1", PlayerName = "Mark's card", Description = "A real card, front and back." });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var card = (await created.Content.ReadFromJsonAsync<CardDto>())!;
        using var form = Form(Picture());
        var uploaded = await owner.PostAsync($"/api/cards/{card.Id}/photos", form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var photo = (await uploaded.Content.ReadFromJsonAsync<CardPhotoDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/" + photo.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/" + photo.ThumbnailUrl)).StatusCode);
        Assert.Equal(0, (await visitor.GetFromJsonAsync<PageResult<MarketplaceCardDto>>("/api/collection"))!.TotalCount);
        card = (await owner.GetFromJsonAsync<CardDto>($"/api/cards/{card.Id}"))!;
        var update = MarksBaseballCards.Client.Services.ApiClient.Editable(card); update.IsPublished = true;
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/cards/{card.Id}", update)).StatusCode);
        Assert.Equal(1, (await visitor.GetFromJsonAsync<PageResult<MarketplaceCardDto>>("/api/collection"))!.TotalCount);
        Assert.Equal(0, (await visitor.GetFromJsonAsync<PageResult<MarketplaceCardDto>>("/api/marketplace/page"))!.TotalCount);
        var publicImage = await visitor.GetAsync("/" + photo.Url);
        Assert.Equal("image/jpeg", publicImage.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", publicImage.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/cards/{card.Id}/photos/{photo.Id}")).StatusCode);
        card = (await owner.GetFromJsonAsync<CardDto>($"/api/cards/{card.Id}"))!;
        update = MarksBaseballCards.Client.Services.ApiClient.Editable(card); update.IsForSale = true; update.Price = 12.50m;
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/cards/{card.Id}", update)).StatusCode);
        Assert.Equal(1, (await visitor.GetFromJsonAsync<PageResult<MarketplaceCardDto>>("/api/marketplace/page"))!.TotalCount);
        card = (await owner.GetFromJsonAsync<CardDto>($"/api/cards/{card.Id}"))!;
        update = MarksBaseballCards.Client.Services.ApiClient.Editable(card);
        update.IsPublished = false; update.IsForSale = false;
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/cards/{card.Id}", update)).StatusCode);
        Assert.Equal(0, (await visitor.GetFromJsonAsync<PageResult<MarketplaceCardDto>>("/api/collection"))!.TotalCount);
        Assert.Equal(0, (await visitor.GetFromJsonAsync<PageResult<MarketplaceCardDto>>("/api/marketplace/page"))!.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/" + photo.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/" + photo.ThumbnailUrl)).StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("SystemAdmin", HttpStatusCode.Forbidden)]
    public async Task OnlyInventoryAdminsCanCreateOrUpload(string? role, HttpStatusCode expected)
    {
        using var host = new TestHost(); await host.InitializeAsync(); using var client = host.Client(role);
        Assert.Equal(expected, (await client.PostAsJsonAsync("/api/cards", new CardUpsertDto { CardNumber = "1", PlayerName = "Player" })).StatusCode);
        using var form = Form(Picture());
        Assert.Equal(expected, (await client.PostAsync("/api/cards/1/photos", form)).StatusCode);
    }

    [Fact]
    public async Task AcceptsThirtyMegabytePhotoAndRejectsOneByteOver()
    {
        using var host = new TestHost(); await host.InitializeAsync(); using var owner = host.Client("Admin");
        var created = await owner.PostAsJsonAsync("/api/cards", new CardUpsertDto { CardNumber = "30MB", PlayerName = "Large photo" });
        var card = (await created.Content.ReadFromJsonAsync<CardDto>())!;
        // A valid PNG followed by extra input data. Re-encoding must discard the original payload.
        var bytes = new byte[MarksBaseballCards.Shared.Common.CardPhotoLimits.MaxFileBytes];
        Picture().CopyTo(bytes, 0);
        using (var form = Form(bytes))
        {
            var result = await owner.PostAsync($"/api/cards/{card.Id}/photos", form);
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            var photo = (await result.Content.ReadFromJsonAsync<CardPhotoDto>())!;
            var stored = await owner.GetByteArrayAsync("/" + photo.Url);
            Assert.True(stored.Length < 100_000); // Never publish the padded original.
        }
        using var oversized = Form(new byte[MarksBaseballCards.Shared.Common.CardPhotoLimits.MaxFileBytes + 1]);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/api/cards/{card.Id}/photos", oversized)).StatusCode);
    }

    [Fact]
    public async Task BrowserRoleClaimsDoNotGrantAccessWithoutAValidSignature()
    {
        using var host = new TestHost(); await host.InitializeAsync(); using var client = host.Client("SystemAdmin");
        var parts = client.DefaultRequestHeaders.Authorization!.Parameter!.Split('.');
        var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1].Replace('-', '+').Replace('_', '/').PadRight((parts[1].Length + 3) / 4 * 4, '=')));
        parts[1] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload.Replace("SystemAdmin", "Admin", StringComparison.Ordinal)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cards/page")).StatusCode);
        using var form = Form(Picture());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/cards/1/photos", form)).StatusCode);
    }

    [Fact]
    public async Task RejectsActivePayloadAndPublicationWithoutPhotographs()
    {
        using var host = new TestHost(); await host.InitializeAsync(); using var owner = host.Client("Admin");
        var response = await owner.PostAsJsonAsync("/api/cards", new CardUpsertDto { CardNumber = "2", PlayerName = "Player", Description = "Description" });
        var card = (await response.Content.ReadFromJsonAsync<CardDto>())!;
        using var form = Form(System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'></svg>"));
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/api/cards/{card.Id}/photos", form)).StatusCode);
        var update = MarksBaseballCards.Client.Services.ApiClient.Editable(card); update.IsPublished = true;
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/cards/{card.Id}", update)).StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(host.DirectoryPath, "photos")));
    }

    [Theory]
    [InlineData("/api/collection?pageSize=100000")]
    [InlineData("/api/collection?page=0")]
    [InlineData("/api/marketplace/page?sort=untrusted")]
    public async Task BoundsPublicQueries(string url)
    {
        using var host = new TestHost(); await host.InitializeAsync(); using var client = host.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task EnforcesSixPhotoQuotaAndDoesNotAcceptAnotherCardsPhotoId()
    {
        using var host = new TestHost(); await host.InitializeAsync(); using var owner = host.Client("Admin");
        var response = await owner.PostAsJsonAsync("/api/cards", new CardUpsertDto { CardNumber = "7", PlayerName = "Player", Description = "Card" });
        var card = (await response.Content.ReadFromJsonAsync<CardDto>())!;
        for (var i = 0; i < 6; i++) { using var form = Form(Picture()); Assert.Equal(HttpStatusCode.Created, (await owner.PostAsync($"/api/cards/{card.Id}/photos", form)).StatusCode); }
        using var extra = Form(Picture());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/cards/{card.Id}/photos", extra)).StatusCode);
        card = (await owner.GetFromJsonAsync<CardDto>($"/api/cards/{card.Id}"))!;
        Assert.Equal(6, card.PhotoCount);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsync($"/api/cards/{card.Id + 1}/photos/{card.Photos[0].Id}/cover", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/cards/{card.Id}")).StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(host.DirectoryPath, "photos")));
    }
}
