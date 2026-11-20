// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;

namespace Vercel.Oidc.Tests;

/// <summary>
/// The <c>beforeEach</c> of upstream's tests: a temporary project root with a Vercel CLI data directory, the <c>TokenIo</c> seams
/// pointed at it, and a fake Vercel API behind the HTTP client. The tests change process-wide state, so they run in
/// <see cref="EnvironmentCollection"/>.
/// </summary>
[Collection(EnvironmentCollection.Name)]
public abstract class OidcTestBase : IDisposable
{
    protected const string ProjectId = "prj_test123";
    protected const string TeamId = "team_test456";

    private readonly string? _previousToken = Environment.GetEnvironmentVariable("VERCEL_OIDC_TOKEN");

    protected OidcTestBase()
    {
        RootDir = Path.Combine(Path.GetTempPath(), "test-" + Guid.NewGuid());
        UserDataDir = Path.Combine(RootDir, "data");
        CliDataDir = Path.Combine(UserDataDir, "com.vercel.cli");
        TokenDataDir = Path.Combine(UserDataDir, "com.vercel.token");
        Directory.CreateDirectory(CliDataDir);
        Directory.CreateDirectory(TokenDataDir);
        Directory.CreateDirectory(Path.Combine(RootDir, ".vercel"));
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", null);
        TokenIo.FindRootDirOverride = () =>
        {
            FindRootDirCalls++;
            return RootDir;
        };
        TokenIo.GetUserDataDirOverride = () => UserDataDir;
        OAuth.HandlerOverride = Api;
        OAuth.ResetTokenEndpointCache();
    }

    protected string RootDir { get; }

    protected string UserDataDir { get; }

    protected string CliDataDir { get; }

    protected string TokenDataDir { get; }

    protected FakeVercelApi Api { get; } = new();

    /// <summary>How many times the code looked for the project root.</summary>
    protected int FindRootDirCalls { get; private set; }

    public void Dispose()
    {
        TokenIo.FindRootDirOverride = null;
        TokenIo.GetUserDataDirOverride = null;
        Vercel.Oidc.TokenUtil.GetTokenPayloadOverride = null;
        OAuth.HandlerOverride = null;
        OAuth.ResetTokenEndpointCache();
        Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", _previousToken);
        Api.Dispose();
        if (Directory.Exists(RootDir))
        {
            Directory.Delete(RootDir, recursive: true);
        }
    }

    protected void WriteAuthJson(string json) => File.WriteAllText(Path.Combine(CliDataDir, "auth.json"), json);

    protected string ReadAuthJson() => File.ReadAllText(Path.Combine(CliDataDir, "auth.json"));

    protected void WriteProjectJson(string projectId, string? orgId = null) =>
        File.WriteAllText(
            Path.Combine(RootDir, ".vercel", "project.json"),
            orgId is null
                ? JsonSerializer.Serialize(new { projectId })
                : JsonSerializer.Serialize(new { projectId, orgId })
        );

    protected static void SetOidcToken(string? token) => Environment.SetEnvironmentVariable("VERCEL_OIDC_TOKEN", token);

    protected static string? GetOidcToken() => Environment.GetEnvironmentVariable("VERCEL_OIDC_TOKEN");

    protected static string CreateExpiredToken() => CreateToken(exp: 1, iat: 1, signature: "fake_signature");

    protected static string CreateValidToken(string value = "valid-token")
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
