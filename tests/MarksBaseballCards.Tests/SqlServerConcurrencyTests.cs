using System.Net;
using System.Net.Http.Json;
using MarksBaseballCards.Shared.Models;
using Xunit;

namespace MarksBaseballCards.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MARKS_TEST_SQLSERVER") is null)
            Skip = "Requires isolated SQL Server; SQLite cannot certify SQL rowversion behavior.";
    }
}
public sealed class SqlServerConcurrencyTests
{
    [SqlServerFact]
    public async Task RejectsLostUpdatesUsingActualSqlRowversion()
    {
        using var host = new TestHost(); await host.InitializeAsync(); using var owner = host.Client("Admin");
        var created = await owner.PostAsJsonAsync("api/cards", new CardUpsertDto { CardNumber = "19", PlayerName = "Player" });
        var card = (await created.Content.ReadFromJsonAsync<CardDto>())!;
        Assert.Equal(8, card.RowVersion!.Length);
        var first = MarksBaseballCards.Client.Services.ApiClient.Editable(card);
        var stale = MarksBaseballCards.Client.Services.ApiClient.Editable(card);
        first.Description = "First edit"; stale.Description = "Stale edit";
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"api/cards/{card.Id}", first)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"api/cards/{card.Id}", stale)).StatusCode);
        Assert.Equal("First edit", (await owner.GetFromJsonAsync<CardDto>($"api/cards/{card.Id}"))!.Description);
    }
}
