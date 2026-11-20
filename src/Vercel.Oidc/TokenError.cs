// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

namespace Vercel.Oidc;

/// <summary>Maps to <c>VercelOidcTokenError</c> in <c>@vercel/oidc</c> <c>token-error.ts</c>.</summary>
public sealed class VercelOidcTokenError : Exception
{
    public VercelOidcTokenError(string message, Exception? cause = null)
        : base(message, cause!) { }
}
