using System.Net;

namespace PlayStead.CatalogBuilder.Tests;

public sealed class IgdbClientAuthenticationTests
{
    [Fact]
    public async Task Twitch_oauth_uses_form_body_and_parses_token()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/oauth2/token", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"token-value\"}") }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        using var client = new HttpClient(handler);
        Environment.SetEnvironmentVariable("IGDB_CLIENT_ID", "client-id");
        Environment.SetEnvironmentVariable("IGDB_CLIENT_SECRET", "secret-value");
        try
        {
            var result = await IgdbClient.FetchAsync(client);
            Assert.Empty(result);
            Assert.Equal("https://id.twitch.tv/oauth2/token", handler.OAuthRequestUri?.ToString());
            Assert.Contains("client_id=client-id", handler.OAuthBody);
            Assert.Contains("client_secret=secret-value", handler.OAuthBody);
            Assert.Contains("grant_type=client_credentials", handler.OAuthBody);
        }
        finally { Environment.SetEnvironmentVariable("IGDB_CLIENT_ID", null); Environment.SetEnvironmentVariable("IGDB_CLIENT_SECRET", null); }
    }

    [Fact]
    public async Task Twitch_oauth_failure_is_sanitized()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("invalid client_secret=secret-value access_token=token-value") });
        using var client = new HttpClient(handler);
        Environment.SetEnvironmentVariable("IGDB_CLIENT_ID", "client-id");
        Environment.SetEnvironmentVariable("IGDB_CLIENT_SECRET", "secret-value");
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => IgdbClient.FetchAsync(client));
            Assert.Contains("HTTP 400", error.Message);
            Assert.DoesNotContain("secret-value", error.Message);
            Assert.DoesNotContain("client-id", error.Message);
            Assert.DoesNotContain("token-value", error.Message);
        }
        finally { Environment.SetEnvironmentVariable("IGDB_CLIENT_ID", null); Environment.SetEnvironmentVariable("IGDB_CLIENT_SECRET", null); }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public Uri? OAuthRequestUri { get; private set; }
        public string OAuthBody { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri?.AbsolutePath.EndsWith("/oauth2/token", StringComparison.Ordinal) == true)
            {
                OAuthRequestUri = request.RequestUri;
                OAuthBody = body;
            }
            return responder(request);
        }
    }
}
