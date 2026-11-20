// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace Vercel.Oidc.Tests.GetVercelOidcToken;

/// <summary>
/// Port of <c>get-vercel-oidc-token.test.ts</c> &gt; <c>getVercelOidcToken - Error Scenarios</c> (<c>@vercel/oidc</c> 3.2.0).
/// Upstream mocks the <c>token-util</c> functions. The port serves the Vercel API through <see cref="FakeVercelApi"/> and uses
/// real tokens, so the same calls are checked on the requests the code sends.
/// </summary>
public sealed class GetVercelOidcTokenErrorScenariosTests : OidcTestBase
{
    private const string AuthJson = """{"token":"test-auth-token"}""";

    [Fact]
    public async Task Should_throw_AccessTokenMissingError_when_CLI_auth_file_is_missing()
    {
        WriteProjectJson(ProjectId, TeamId);
        SetOidcToken(CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("No authentication found", error.Message);
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_project_json_is_missing()
    {
        WriteAuthJson(AuthJson);
        SetOidcToken(CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("project.json not found, have you linked your project with `vc link?`", error.Message);
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_root_directory_cannot_be_found()
    {
        TokenIo.FindRootDirOverride = () => null;
        SetOidcToken(CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains(
            "Unable to find project root directory. Have you linked your project with `vc link?`",
            error.Message
        );
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_user_data_directory_cannot_be_found()
    {
        WriteProjectJson(ProjectId, TeamId);
        TokenIo.GetUserDataDirOverride = () => null;
        SetOidcToken(CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("Unable to find user data directory. Please reach out to Vercel support.", error.Message);
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_API_returns_non_200()
    {
        WriteAuthJson(AuthJson);
        WriteProjectJson(ProjectId, TeamId);
        Api.OnProjectToken = _ => FakeVercelApi.Json(HttpStatusCode.Unauthorized, "{}");
        SetOidcToken(CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("Failed to refresh OIDC token: Unauthorized", error.Message);
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_token_response_is_malformed()
    {
        WriteAuthJson(AuthJson);
        WriteProjectJson(ProjectId, TeamId);
        Api.OnProjectToken = _ => FakeVercelApi.Json(HttpStatusCode.OK, """{"unexpected":true}""");
        SetOidcToken(CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains(
            "Vercel OIDC token is malformed. Expected a string-valued token property. Please run `vc env pull` and try again",
            error.Message
        );
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_token_has_invalid_format()
    {
        SetOidcToken("not-a-valid-jwt-token");
        Vercel.Oidc.TokenUtil.GetTokenPayloadOverride = _ =>
            throw new InvalidOperationException("Invalid token. Please run `vc env pull` and try again");

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("Invalid token. Please run `vc env pull` and try again", error.Message);
    }

    [Fact]
    public async Task Should_throw_error_when_token_expiry_check_fails()
    {
        SetOidcToken("not-a-jwt-token");

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("Invalid token. Please run `vc env pull` and try again", error.Message);
    }

    [Fact]
    public async Task Should_fail_when_no_token_exists_and_no_CLI_credentials_available()
    {
        // Upstream sets process.env.VERCEL_OIDC_TOKEN = undefined, which Node stores as the string "undefined".
        SetOidcToken("undefined");

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("Invalid token", error.Message);
    }

    [Fact]
    public async Task Should_propagate_filesystem_errors_when_saving_token_fails()
    {
        WriteAuthJson(AuthJson);
        WriteProjectJson(ProjectId, TeamId);
        Api.OnProjectToken = _ => FakeVercelApi.Json(HttpStatusCode.OK, """{"token":"new-valid-token"}""");
        // Upstream makes saveToken throw EACCES. A directory in the place of the cache file makes the save fail on every OS and for root,
        // and the message of that error differs by OS, so the test checks that the filesystem error itself is propagated.
        Directory.CreateDirectory(Path.Combine(TokenDataDir, ProjectId + ".json"));
        SetOidcToken(CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.True(
            error.InnerException is IOException or UnauthorizedAccessException,
            error.InnerException?.ToString()
        );
    }

    [Fact]
    public async Task Should_succeed_when_valid_token_exists_in_env()
    {
        var validToken = CreateValidToken();
        SetOidcToken(validToken);

        var token = await GetTokenAsync();

        Assert.Equal(validToken, token);
    }

    [Fact]
    public async Task Should_refresh_when_token_is_expired_but_all_configs_are_valid()
    {
        WriteAuthJson(AuthJson);
        WriteProjectJson(ProjectId, TeamId);
        var newToken = CreateValidToken("new-token");
        Api.OnProjectToken = _ => FakeVercelApi.Json(HttpStatusCode.OK, $$"""{"token":"{{newToken}}"}""");
        SetOidcToken(CreateExpiredToken());

        var token = await GetTokenAsync();

        Assert.Equal(newToken, token);
    }

    [Fact]
    public async Task Should_use_provided_team_and_project_for_refresh_instead_of_reading_project_json()
    {
        const string customProjectId = "prj_custom123";
        const string customTeamId = "team_custom456";
        WriteAuthJson(AuthJson);
        var newToken = CreateValidToken("custom-token");
        Api.OnProjectToken = _ => FakeVercelApi.Json(HttpStatusCode.OK, $$"""{"token":"{{newToken}}"}""");
        SetOidcToken(CreateExpiredToken());

        var token = await GetTokenAsync(new VercelOidcTokenOptions { Team = customTeamId, Project = customProjectId });

        Assert.Equal(newToken, token);
        AssertProjectTokenRequest("test-auth-token", customProjectId, customTeamId);
    }

    [Fact]
    public async Task Should_use_provided_project_and_read_team_from_project_json()
    {
        const string customProjectId = "prj_custom234";
        WriteAuthJson(AuthJson);
        WriteProjectJson("original-project-id", TeamId);
        var newToken = CreateValidToken("partial-custom-token");
        Api.OnProjectToken = _ => FakeVercelApi.Json(HttpStatusCode.OK, $$"""{"token":"{{newToken}}"}""");
        SetOidcToken(CreateExpiredToken());

        var token = await GetTokenAsync(new VercelOidcTokenOptions { Project = customProjectId });

        Assert.Equal(newToken, token);
        AssertProjectTokenRequest("test-auth-token", customProjectId, TeamId);
    }

    [Fact]
    public async Task Should_use_provided_team_and_read_project_from_project_json()
    {
        const string customTeamId = "team_custom789";
        WriteAuthJson(AuthJson);
        WriteProjectJson(ProjectId, "original-team-id");
        var newToken = CreateValidToken("partial-custom-token-2");
        Api.OnProjectToken = _ => FakeVercelApi.Json(HttpStatusCode.OK, $$"""{"token":"{{newToken}}"}""");
        SetOidcToken(CreateExpiredToken());

        var token = await GetTokenAsync(new VercelOidcTokenOptions { Team = customTeamId });

        Assert.Equal(newToken, token);
        AssertProjectTokenRequest("test-auth-token", ProjectId, customTeamId);
    }

    [Fact]
    public async Task Should_not_refresh_when_token_is_valid_even_with_options_provided()
    {
        var validToken = CreateValidToken();
        SetOidcToken(validToken);
        Api.OnProjectToken = _ => FakeVercelApi.Json(HttpStatusCode.OK, """{"token":"should-not-be-called"}""");

        var token = await GetTokenAsync(
            new VercelOidcTokenOptions { Team = "custom-team", Project = "custom-project" }
        );

        Assert.Equal(validToken, token);
        Assert.Empty(Api.ProjectTokenRequests);
    }

    private static Task<string> GetTokenAsync(VercelOidcTokenOptions? options = null) =>
        VercelOidcToken.GetVercelOidcTokenAsync(options, CancellationToken.None);

    /// <summary>The port's <c>expect(getVercelOidcTokenSpy).toHaveBeenCalledWith(authToken, projectId, teamId)</c>.</summary>
    private void AssertProjectTokenRequest(string authToken, string projectId, string teamId)
    {
        var request = Assert.Single(Api.ProjectTokenRequests);
        Assert.Equal("Bearer " + authToken, request.Authorization);
        Assert.Equal("/v1/projects/" + projectId + "/token", request.Uri.AbsolutePath);
        Assert.Contains("teamId=" + teamId, request.Uri.Query);
    }
}
