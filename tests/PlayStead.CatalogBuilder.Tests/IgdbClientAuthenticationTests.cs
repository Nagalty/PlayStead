using System.Net;

namespace PlayStead.CatalogBuilder.Tests;

public sealed class IgdbClientAuthenticationTests
{
    [Fact]
    public async Task Igdb_page_uses_complete_fields_and_retries_429()
    {
        var calls = 0;
        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/oauth2/token", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"token-value\"}") };
            calls++;
            if (calls == 1) return new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero) } };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"id\":42,\"name\":\"Dune: Awakening\",\"first_release_date\":1700000000,\"involved_companies\":[{\"company\":{\"name\":\"Funcom\"},\"developer\":true,\"publisher\":true}],\"genres\":[{\"name\":\"RPG\"}],\"external_games\":[{\"external_game_source\":{\"name\":\"Steam\"},\"uid\":\"1172710\"}]}]")
            };
        });
        using var client = new HttpClient(handler);
        Environment.SetEnvironmentVariable("IGDB_CLIENT_ID", "client-id");
        Environment.SetEnvironmentVariable("IGDB_CLIENT_SECRET", "secret-value");
        try
        {
            var result = await IgdbClient.FetchAsync(client);
            Assert.Single(result);
            Assert.Contains("Dune", result[0].Name);
            Assert.Equal("Funcom", result[0].Developer);
            Assert.Contains("RPG", result[0].Genres!);
            Assert.Equal("1172710", result[0].ExternalGames![0].ExternalId);
            Assert.All(handler.IgdbBodies, body => Assert.Contains("involved_companies", body));
            Assert.All(handler.IgdbBodies, body => Assert.Contains("external_games", body));
            Assert.Equal(2, calls);
        }
        finally { Environment.SetEnvironmentVariable("IGDB_CLIENT_ID", null); Environment.SetEnvironmentVariable("IGDB_CLIENT_SECRET", null); }
    }

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
        public List<string> IgdbBodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri?.AbsolutePath.EndsWith("/oauth2/token", StringComparison.Ordinal) == true)
            {
                OAuthRequestUri = request.RequestUri;
                OAuthBody = body;
            }
            else if (request.RequestUri?.AbsolutePath.EndsWith("/games", StringComparison.Ordinal) == true)
                IgdbBodies.Add(body);
            return responder(request);
        }
    }
}
