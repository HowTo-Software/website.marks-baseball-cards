using System.Net.Http.Headers;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;

namespace MarksBaseballCards.Client.Auth;

/// <summary>Attaches the stored bearer token to every outgoing API request.</summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly TokenStore _store;
    private readonly NavigationManager _navigation;

    public BearerTokenHandler(TokenStore store, NavigationManager navigation) { _store = store; _navigation = navigation; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var origin = new Uri(_navigation.BaseUri);
        if (request.RequestUri is { IsAbsoluteUri: true } target &&
            (target.Scheme != origin.Scheme || target.Host != origin.Host || target.Port != origin.Port))
        {
            request.Headers.Authorization = null;
            return await base.SendAsync(request, cancellationToken);
        }
        string? token = null;
        try { token = await _store.GetAsync(); }
        catch (JSException)
        {
            // Public reads remain available when browser storage is disabled.
            // Protected endpoints still enforce authentication on the server.
        }
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
