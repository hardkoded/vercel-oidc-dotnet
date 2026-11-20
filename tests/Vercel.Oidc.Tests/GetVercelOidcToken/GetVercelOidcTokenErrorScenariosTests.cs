// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Vercel.Oidc;

namespace Vercel.Oidc.Tests.GetVercelOidcToken;

/// <summary>Port of <c>get-vercel-oidc-token.test.ts</c> &gt; <c>getVercelOidcToken - Error Scenarios</c> (<c>@vercel/oidc</c> 3.2.0).</summary>
[Collection(EnvironmentCollection.Name)]
public sealed class GetVercelOidcTokenErrorScenariosTests : IDisposable
{
    private const string ProjectId = "prj_test123";
    private const string TeamId = "team_test456";

    private readonly string _rootDir;
    private readonly string _userDataDir;
    private readonly string _cliDataDir;
    private readonly string? _previousToken = Environment.GetEnvironmentVariable("VERCEL_OIDC_TOKEN");

    public GetVercelOidcTokenErrorScenariosTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "test-" + Guid.NewGuid());
        _userDataDir = Path.Combine(_rootDir, "data");
        _cliDataDir = Path.Combine(_userDataDir, "com.vercel.cli");
        Directory.CreateDirectory(_cliDataDir);
        Directory.CreateDirectory(Path.Combine(_userDataDir, "com.vercel.token"));
        Directory.CreateDirectory(Path.Combine(_rootDir, ".vercel"));
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", null);
        TokenIo.FindRootDirOverride = () => _rootDir;
        TokenIo.GetUserDataDirOverride = () => _userDataDir;
    }

    public void Dispose()
    {
        TokenIo.FindRootDirOverride = null;
        TokenIo.GetUserDataDirOverride = null;
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", _previousToken);
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task Should_throw_AccessTokenMissingError_when_CLI_auth_file_is_missing()
    {
        WriteProjectJson();
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("No authentication found", error.Message);
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_project_json_is_missing()
    {
        File.WriteAllText(Path.Combine(_cliDataDir, "auth.json"), """{"token":"test-auth-token"}""");
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("project.json not found, have you linked your project with `vc link?`", error.Message);
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_root_directory_cannot_be_found()
    {
        TokenIo.FindRootDirOverride = () => null;
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains(
            "Unable to find project root directory. Have you linked your project with `vc link?`",
            error.Message
        );
    }

    [Fact]
    public async Task Should_throw_helpful_error_when_user_data_directory_cannot_be_found()
    {
        WriteProjectJson();
        TokenIo.GetUserDataDirOverride = () => null;
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", CreateExpiredToken());

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("Unable to find user data directory. Please reach out to Vercel support.", error.Message);
    }

    [Fact]
    public async Task Should_throw_error_when_token_expiry_check_fails()
    {
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", "not-a-jwt-token");

        var error = await Assert.ThrowsAsync<VercelOidcTokenError>(() => GetTokenAsync());

        Assert.Contains("Invalid token. Please run `vc env pull` and try again", error.Message);
    }

    [Fact]
    public async Task Should_succeed_when_valid_token_exists_in_env()
    {
        var validToken = CreateValidToken();
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", validToken);

        var token = await GetTokenAsync();

        Assert.Equal(validToken, token);
    }

    private static Task<string> GetTokenAsync() =>
        VercelOidcToken.GetVercelOidcTokenAsync(null, CancellationToken.None);

    private void WriteProjectJson() =>
        File.WriteAllText(
            Path.Combine(_rootDir, ".vercel", "project.json"),
            JsonSerializer.Serialize(new { projectId = ProjectId, orgId = TeamId })
        );

    private static string CreateExpiredToken() => CreateToken(exp: 1, iat: 1, signature: "fake_signature");

    private static string CreateValidToken(string value = "valid-token")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return CreateToken(exp: now + 43200, iat: now, signature: "fake_signature_" + value);
    }

    private static string CreateToken(long exp, long iat, string signature)
    {
        var header = Base64Url("""{"alg":"RS256","typ":"JWT"}""");
        var payload = Base64Url(
            JsonSerializer.Serialize(
                new
                {
                    sub = "test-sub",
                    exp,
                    iat,
                }
            )
        );
        return header + "." + payload + "." + signature;
    }

    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
