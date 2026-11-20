// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

namespace Vercel.Oidc.Tests;

/// <summary>.NET-only: <c>VercelRequestContext</c> stands in for the <c>@vercel/request-context</c> object that <c>getContext</c> reads.</summary>
[Collection(EnvironmentCollection.Name)]
public sealed class VercelRequestContextTests
{
    [Fact]
    public void Has_no_headers_outside_a_scope()
    {
        Assert.Null(VercelRequestContext.GetContextHeader("x-vercel-id"));
    }

    [Fact]
    public void Reads_a_header_of_the_scope_case_insensitively()
    {
        using var _ = VercelRequestContext.Use(new Dictionary<string, string?> { ["X-Vercel-Id"] = "req_1" });

        Assert.Equal("req_1", VercelRequestContext.GetContextHeader("x-vercel-id"));
    }

    [Fact]
    public void Reads_a_header_of_a_callback_with_a_case_sensitive_dictionary()
    {
        using var _ = VercelRequestContext.Use(() => new Dictionary<string, string?> { ["X-Vercel-Id"] = "req_2" });

        Assert.Equal("req_2", VercelRequestContext.GetContextHeader("x-vercel-id"));
    }

    [Fact]
    public void Treats_a_null_callback_and_a_null_result_as_no_headers()
    {
        using (VercelRequestContext.Use(get: null))
        {
            Assert.Null(VercelRequestContext.GetContextHeader("x-vercel-id"));
        }

        using (VercelRequestContext.Use(() => null))
        {
            Assert.Null(VercelRequestContext.GetContextHeader("x-vercel-id"));
        }
    }

    [Fact]
    public void Restores_the_previous_scope_when_disposed()
    {
        using var outer = VercelRequestContext.Use(new Dictionary<string, string?> { ["x-vercel-id"] = "outer" });
        using (VercelRequestContext.Use(new Dictionary<string, string?> { ["x-vercel-id"] = "inner" }))
        {
            Assert.Equal("inner", VercelRequestContext.GetContextHeader("x-vercel-id"));
        }

        Assert.Equal("outer", VercelRequestContext.GetContextHeader("x-vercel-id"));
    }

    [Fact]
    public void Rejects_header_names_that_differ_only_by_case_with_different_values()
    {
        Assert.Throws<ArgumentException>(() =>
            VercelRequestContext.Use(new Dictionary<string, string?> { ["x-a"] = "1", ["X-A"] = "2" })
        );
    }
}
