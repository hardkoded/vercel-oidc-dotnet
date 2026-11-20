// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;

namespace Vercel.Oidc.Tests;

/// <summary>One request the code under test sent to the fake Vercel API.</summary>
public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string Body);

/// <summary>
/// Stands in for the network that upstream's tests mock with <c>vi.spyOn(tokenUtil, "getVercelOidcToken")</c> and
/// <c>vi.spyOn(oauth, "refreshTokenRequest")</c>: the OAuth discovery, the token endpoint, and the project token API.
/// </summary>
public sealed class FakeVercelApi : HttpMessageHandler
{
    private readonly List<RecordedRequest> _requests = [];

    public IReadOnlyList<RecordedRequest> Requests => _requests;

    /// <summary>Answers <c>POST /v1/projects/{id}/token</c>. The default gives <c>{"token":"test-token"}</c>.</summary>
    public Func<RecordedRequest, HttpResponseMessage> OnProjectToken { get; set; } =
        _ => Json(HttpStatusCode.OK, """{"token":"test-token"}""");

    /// <summary>Answers the OAuth token endpoint. The default is a server error.</summary>
    public Func<RecordedRequest, HttpResponseMessage> OnTokenEndpoint { get; set; } =
        _ => Json(HttpStatusCode.InternalServerError, "{}");

    /// <summary>The project token API requests.</summary>
    public IEnumerable<RecordedRequest> ProjectTokenRequests =>
        _requests.Where(request => request.Uri.Host == "api.vercel.com");

    /// <summary>The OAuth token endpoint requests.</summary>
    public IEnumerable<RecordedRequest> TokenEndpointRequests =>
        _requests.Where(request => request.Uri.AbsolutePath == "/api/login/oauth/token");

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            body
        );
        _requests.Add(recorded);

        if (recorded.Uri.AbsolutePath == "/.well-known/openid-configuration")
        {
            return Json(HttpStatusCode.OK, """{"token_endpoint":"https://vercel.com/api/login/oauth/token"}""");
        }

        if (recorded.Uri.AbsolutePath == "/api/login/oauth/token")
        {
            return OnTokenEndpoint(recorded);
        }

        if (recorded.Uri.Host == "api.vercel.com")
        {
            return OnProjectToken(recorded);
        }

        throw new InvalidOperationException("Unexpected request: " + recorded.Uri);
    }
}
