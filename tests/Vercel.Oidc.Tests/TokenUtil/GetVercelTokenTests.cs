// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;

namespace Vercel.Oidc.Tests.TokenUtil;

/// <summary>
/// Port of <c>token-util.test.ts</c> &gt; <c>getVercelToken</c> (<c>@vercel/oidc</c> 3.2.0). Upstream mocks <c>readAuthConfig</c>,
/// <c>writeAuthConfig</c> and <c>refreshTokenRequest</c>. The port uses a real <c>auth.json</c> and serves the OAuth token endpoint through
/// <see cref="FakeVercelApi"/>, so the writes are checked on the file.
/// </summary>
public sealed class GetVercelTokenTests : OidcTestBase
{
    private static readonly long s_now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [Fact]
    public async Task Should_return_token_if_valid_and_not_expired()
    {
        var json = $$"""{"token":"valid-access-token","refreshToken":"refresh-token","expiresAt":{{s_now + 3600}}}""";
        WriteAuthJson(json);

        var token = await GetVercelTokenAsync();

        Assert.Equal("valid-access-token", token);
        Assert.Equal(json, ReadAuthJson());
    }

    [Fact]
    public async Task Should_throw_AccessTokenMissingError_if_auth_config_does_not_exist()
    {
        await Assert.ThrowsAsync<AccessTokenMissingError>(() => GetVercelTokenAsync());
    }

    [Fact]
    public async Task Should_refresh_token_if_expired_and_refresh_token_exists()
    {
        WriteExpiredAuth(refreshToken: "valid-refresh-token");
        Api.OnTokenEndpoint = _ =>
            TokenResponse(
                """{"access_token":"new-access-token","token_type":"Bearer","expires_in":3600,"refresh_token":"new-refresh-token"}"""
            );

        var token = await GetVercelTokenAsync();

        Assert.Equal("new-access-token", token);
        var request = Assert.Single(Api.TokenEndpointRequests);
        Assert.Contains("refresh_token=valid-refresh-token", request.Body);
        var written = JsonNode.Parse(ReadAuthJson())!;
        Assert.Equal("new-access-token", (string?)written["token"]);
        Assert.Equal("new-refresh-token", (string?)written["refreshToken"]);
        Assert.NotNull(written["expiresAt"]);
    }

    [Fact]
    public async Task Should_clear_auth_and_throw_RefreshAccessTokenFailedError_if_token_expired_and_no_refresh_token()
    {
        WriteExpiredAuth(refreshToken: null);

        await Assert.ThrowsAsync<RefreshAccessTokenFailedError>(() => GetVercelTokenAsync());

        AssertAuthCleared();
    }

    [Fact]
    public async Task Should_clear_auth_and_throw_RefreshAccessTokenFailedError_if_refresh_fails_with_OAuth_error()
    {
        WriteExpiredAuth(refreshToken: "invalid-refresh-token");
        Api.OnTokenEndpoint = _ =>
            TokenResponse(
                """{"error":"invalid_grant","error_description":"Refresh token expired"}""",
                HttpStatusCode.BadRequest
            );

        await Assert.ThrowsAsync<RefreshAccessTokenFailedError>(() => GetVercelTokenAsync());

        AssertAuthCleared();
    }

    [Fact]
    public async Task Should_clear_auth_and_throw_RefreshAccessTokenFailedError_if_refresh_fails_with_network_error()
    {
        WriteExpiredAuth(refreshToken: "valid-refresh-token");
        Api.OnTokenEndpoint = _ => throw new HttpRequestException("Network error");

        await Assert.ThrowsAsync<RefreshAccessTokenFailedError>(() => GetVercelTokenAsync());

        AssertAuthCleared();
    }

    [Fact]
    public async Task Should_treat_token_as_valid_if_expiresAt_is_missing_token_case()
    {
        var json = """{"token":"cli-provided-token"}""";
        WriteAuthJson(json);

        var token = await GetVercelTokenAsync();

        Assert.Equal("cli-provided-token", token);
        Assert.Equal(json, ReadAuthJson());
    }

    [Fact]
    public async Task Should_preserve_new_refresh_token_if_provided_in_response()
    {
        WriteExpiredAuth(refreshToken: "old-refresh-token");
        Api.OnTokenEndpoint = _ =>
            TokenResponse(
                """{"access_token":"new-access-token","token_type":"Bearer","expires_in":3600,"refresh_token":"new-refresh-token"}"""
            );

        await GetVercelTokenAsync();

        Assert.Equal("new-refresh-token", (string?)JsonNode.Parse(ReadAuthJson())!["refreshToken"]);
    }

    [Fact]
    public async Task Should_not_overwrite_refresh_token_if_not_provided_in_response()
    {
        WriteExpiredAuth(refreshToken: "existing-refresh-token");
        Api.OnTokenEndpoint = _ =>
            TokenResponse("""{"access_token":"new-access-token","token_type":"Bearer","expires_in":3600}""");

        await GetVercelTokenAsync();

        Assert.Null(JsonNode.Parse(ReadAuthJson())!.AsObject()["refreshToken"]);
    }

    [Fact]
    public async Task Should_calculate_expiresAt_correctly_from_expires_in()
    {
        WriteExpiredAuth(refreshToken: "valid-refresh-token");
        Api.OnTokenEndpoint = _ =>
            TokenResponse("""{"access_token":"new-access-token","token_type":"Bearer","expires_in":7200}""");

        var beforeCall = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await GetVercelTokenAsync();
        var afterCall = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var expiresAt = (double)JsonNode.Parse(ReadAuthJson())!["expiresAt"]!;
        Assert.True(expiresAt >= beforeCall + 7200);
        Assert.True(expiresAt <= afterCall + 7200);
    }

    private static Task<string> GetVercelTokenAsync() =>
        Vercel.Oidc.TokenUtil.GetVercelTokenAsync(null, CancellationToken.None);

    private static HttpResponseMessage TokenResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        FakeVercelApi.Json(status, json);

    private void WriteExpiredAuth(string? refreshToken) =>
        WriteAuthJson(
            refreshToken is null
                ? $$"""{"token":"expired-access-token","expiresAt":{{s_now - 3600}}}"""
                : $$"""{"token":"expired-access-token","refreshToken":"{{refreshToken}}","expiresAt":{{s_now - 3600}}}"""
        );

    /// <summary>The port's <c>expect(writeAuthConfig).toHaveBeenCalledWith({})</c>.</summary>
    private void AssertAuthCleared() => Assert.Empty(JsonNode.Parse(ReadAuthJson())!.AsObject());
}
