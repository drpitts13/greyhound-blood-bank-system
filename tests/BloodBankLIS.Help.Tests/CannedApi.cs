using System.Net;
using System.Net.Http.Json;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// For rendered page tests: the page's real <see cref="BloodBankApiClient"/>, answered by fixed responses keyed by API
/// path and serialized as the API does. Any other request is answered 404, which the pages treat as nothing to show.
/// A dev-mode <see cref="UserSession"/> grants every permission.
/// </summary>
public static class CannedApi
{
    public static void Register(BunitContext context, IReadOnlyDictionary<string, object> responses)
    {
        var http = new HttpClient(new Handler(responses)) { BaseAddress = new Uri("http://api.test/") };
        context.Services.AddSingleton(new BloodBankApiClient(http));
        context.Services.AddSingleton(new UserSession(new DevModeState { Enabled = true }));
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private sealed class Handler(IReadOnlyDictionary<string, object> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responses.TryGetValue(request.RequestUri!.AbsolutePath, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType(), options: ClinicalJson.WebOptions) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
