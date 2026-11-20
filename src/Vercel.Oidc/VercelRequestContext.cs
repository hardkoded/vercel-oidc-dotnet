// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

namespace Vercel.Oidc;

/// <summary>
/// The ambient Vercel request context. Maps to the <c>@vercel/request-context</c> object that Vercel stores on
/// <c>globalThis[Symbol.for('@vercel/request-context')]</c> and that <c>getContext</c> in <c>@vercel/oidc</c> reads.
/// A .NET host supplies it with <see cref="Use(IReadOnlyDictionary{string, string})"/>, for example from ASP.NET Core middleware.
/// </summary>
public static class VercelRequestContext
{
    private static readonly AsyncLocal<Scope?> s_current = new();

    internal static IReadOnlyDictionary<string, string?>? Headers => s_current.Value?.Headers;

    /// <summary>
    /// Returns a header of the current request context, or null. Maps to <c>getContext().headers?.[name]</c> of <c>@vercel/oidc</c>
    /// (<c>get-context.ts</c>). Header names match case-insensitively, whatever comparer the supplied dictionary has.
    /// </summary>
    public static string? GetContextHeader(string name)
    {
        var headers = Headers;
        if (headers is null)
        {
            return null;
        }

        foreach (var pair in headers)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Makes <paramref name="headers"/> the request headers of the current async flow (for example <c>x-vercel-id</c> and
    /// <c>x-vercel-oidc-token</c>) until the returned scope is disposed. Header names are matched case-insensitively;
    /// names that differ only by case must carry the same value, otherwise <see cref="ArgumentException"/> is thrown.
    /// Disposing the scope also revokes the headers for work that captured them, and disposing twice does nothing.
    /// </summary>
    public static IDisposable Use(IReadOnlyDictionary<string, string?>? headers)
    {
        Dictionary<string, string?>? copy = null;
        if (headers is not null)
        {
            copy = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in headers)
            {
                if (copy.TryGetValue(pair.Key, out var existing) && existing != pair.Value)
                {
                    throw new ArgumentException(
                        $"The header '{pair.Key}' appears more than once with different values.",
                        nameof(headers)
                    );
                }

                copy[pair.Key] = pair.Value;
            }
        }

        var scope = new Scope(copy, s_current.Value);
        s_current.Value = scope;
        return scope;
    }

    /// <summary>
    /// Makes <paramref name="get"/> the provider of the request headers of the current async flow, until the returned
    /// scope is disposed. Maps to the <c>get</c> method of the <c>@vercel/request-context</c> object. The function runs
    /// on every lookup. A null <paramref name="get"/> is a context without a <c>get</c> method, and a function that
    /// returns null is a context without headers: both read as no headers. Header names match case-insensitively.
    /// </summary>
    public static IDisposable Use(Func<IReadOnlyDictionary<string, string?>?>? get)
    {
        var scope = new Scope(get, s_current.Value);
        s_current.Value = scope;
        return scope;
    }

    private sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private Func<IReadOnlyDictionary<string, string?>?>? _get;
        private int _disposed;

        public Scope(IReadOnlyDictionary<string, string?>? headers, Scope? previous)
            : this(headers is null ? null : () => headers, previous) { }

        public Scope(Func<IReadOnlyDictionary<string, string?>?>? get, Scope? previous)
        {
            _get = get;
            _previous = previous;
        }

        public IReadOnlyDictionary<string, string?>? Headers => _get?.Invoke();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _get = null;
            s_current.Value = _previous;
        }
    }
}
