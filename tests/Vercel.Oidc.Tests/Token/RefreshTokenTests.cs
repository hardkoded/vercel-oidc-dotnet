// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace Vercel.Oidc.Tests.Token;

/// <summary>
/// Port of <c>token.test.ts</c> &gt; <c>refreshToken</c> (<c>@vercel/oidc</c> 3.2.0). Upstream mocks <c>getVercelToken</c>,
/// <c>getVercelOidcToken</c> and <c>getTokenPayload</c>. The port reads a real <c>auth.json</c>, serves the project token API through
/// <see cref="FakeVercelApi"/>, and replaces only <c>getTokenPayload</c>, because the tokens of these tests are not JWTs.
/// </summary>
public sealed class RefreshTokenTests : OidcTestBase
{
    public RefreshTokenTests()
    {
        WriteAuthJson("""{"token":"test"}""");
        WriteProjectJson(ProjectId);
        SetPayloadExpiry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 100_000);
    }

    [Fact]
    public async Task Should_correctly_load_saved_token_from_file()
    {
        File.WriteAllText(Path.Combine(TokenDataDir, ProjectId + ".json"), """{"token":"test-saved"}""");

        await RefreshAsync();

        Assert.Equal("test-saved", GetOidcToken());
    }

    [Fact]
    public async Task Should_correctly_save_token_to_file()
    {
        await RefreshAsync();

        Assert.Equal("test-token", GetOidcToken());
        var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(TokenDataDir, ProjectId + ".json")))!;
        Assert.Equal("test-token", (string?)saved["token"]);
        Assert.Single(saved.AsObject());
    }

    [Fact]
    public async Task Should_use_provided_team_and_project_instead_of_reading_project_json()
    {
        const string customProjectId = "prj_custom123";
        const string customTeamId = "team_custom456";

        await RefreshAsync(new VercelOidcTokenOptions { Team = customTeamId, Project = customProjectId });

        Assert.Equal(0, FindRootDirCalls);
        AssertProjectTokenRequest("test", customProjectId, customTeamId);
        var tokenPath = Path.Combine(TokenDataDir, customProjectId + ".json");
        Assert.True(File.Exists(tokenPath));
        Assert.Equal("test-token", (string?)JsonNode.Parse(File.ReadAllText(tokenPath))!["token"]);
    }

    [Fact]
    public async Task Should_merge_provided_project_with_team_from_project_json()
    {
        const string customProjectId = "prj_custom234";
        const string projectTeamId = "team_fromproject567";
        WriteProjectJson(ProjectId, projectTeamId);

        await RefreshAsync(new VercelOidcTokenOptions { Project = customProjectId });

        AssertProjectTokenRequest("test", customProjectId, projectTeamId);
    }

    [Fact]
    public async Task Should_merge_provided_team_with_project_from_project_json()
    {
        const string customTeamId = "team_custom789";
        const string projectProjectId = "prj_fromjson123";
        WriteProjectJson(projectProjectId, "original-team");

        await RefreshAsync(new VercelOidcTokenOptions { Team = customTeamId });

        AssertProjectTokenRequest("test", projectProjectId, customTeamId);
    }

    [Fact]
    public async Task Should_use_cached_token_when_valid_with_custom_project()
    {
        const string customProjectId = "prj_customcached123";
        const string customTeamId = "team_customcached456";
        File.WriteAllText(Path.Combine(TokenDataDir, customProjectId + ".json"), """{"token":"cached-valid-token"}""");

        await RefreshAsync(new VercelOidcTokenOptions { Team = customTeamId, Project = customProjectId });

        Assert.Empty(Api.ProjectTokenRequests);
        Assert.Equal("cached-valid-token", GetOidcToken());
    }

    [Fact]
    public async Task Should_refresh_token_when_cached_token_expires_within_buffer()
    {
        const string customProjectId = "prj_buffertest123";
        const string customTeamId = "team_buffertest456";
        var tokenPath = Path.Combine(TokenDataDir, customProjectId + ".json");
        File.WriteAllText(tokenPath, """{"token":"expiring-soon-token"}""");
        SetPayloadExpiry(DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds());
        Api.OnProjectToken = _ => FakeVercelApi.Json(System.Net.HttpStatusCode.OK, """{"token":"fresh-token"}""");

        await RefreshAsync(
            new VercelOidcTokenOptions
            {
                Team = customTeamId,
                Project = customProjectId,
                ExpirationBufferMs = 300_000,
            }
        );

        AssertProjectTokenRequest("test", customProjectId, customTeamId);
        Assert.Equal("fresh-token", GetOidcToken());
        Assert.Equal("fresh-token", (string?)JsonNode.Parse(File.ReadAllText(tokenPath))!["token"]);
    }

    [Fact]
    public async Task Should_use_cached_token_when_it_does_not_expire_within_buffer()
    {
        const string customProjectId = "prj_buffervalid123";
        const string customTeamId = "team_buffervalid456";
        File.WriteAllText(Path.Combine(TokenDataDir, customProjectId + ".json"), """{"token":"valid-cached-token"}""");
        SetPayloadExpiry(DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());

        await RefreshAsync(
            new VercelOidcTokenOptions
            {
                Team = customTeamId,
                Project = customProjectId,
                ExpirationBufferMs = 300_000,
            }
        );

        Assert.Empty(Api.ProjectTokenRequests);
        Assert.Equal("valid-cached-token", GetOidcToken());
    }

    private static Task RefreshAsync(VercelOidcTokenOptions? options = null) =>
        Vercel.Oidc.Token.RefreshTokenAsync(options, CancellationToken.None);

    private static void SetPayloadExpiry(double exp) =>
        Vercel.Oidc.TokenUtil.GetTokenPayloadOverride = _ => new TokenPayload("test-sub", "test-name", exp);

    /// <summary>The port's <c>expect(getVercelOidcTokenSpy).toHaveBeenCalledWith(authToken, projectId, teamId)</c>.</summary>
    private void AssertProjectTokenRequest(string authToken, string projectId, string teamId)
    {
        var request = Assert.Single(Api.ProjectTokenRequests);
        Assert.Equal("Bearer " + authToken, request.Authorization);
        Assert.Equal("/v1/projects/" + projectId + "/token", request.Uri.AbsolutePath);
        Assert.Contains("teamId=" + teamId, request.Uri.Query);
    }
}
