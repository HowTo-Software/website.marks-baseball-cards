using MarksBaseballCards.Client;
using MarksBaseballCards.Client.Auth;
using MarksBaseballCards.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ---- Auth ----
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<ClientAuthService>();
builder.Services.AddAuthorizationCore();

// ---- HTTP client with bearer-token handler ----
builder.Services.AddScoped<BearerTokenHandler>();
builder.Services.AddHttpClient("MarksApi", client =>
    {
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        client.Timeout = TimeSpan.FromSeconds(20);
    })
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("MarksApi"));
builder.Services.AddHttpClient("MarksUploads", client =>
    { client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress); client.Timeout = TimeSpan.FromSeconds(60); })
    .AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<CollectionCatalog>();
builder.Services.AddScoped<UiPreferences>();

var host = builder.Build();
await host.Services.GetRequiredService<UiPreferences>().InitializeAsync();
await host.RunAsync();
