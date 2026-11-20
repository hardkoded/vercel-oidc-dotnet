# Vercel.Oidc

Community .NET port of [`@vercel/oidc`](https://www.npmjs.com/package/@vercel/oidc) (version 3.2.0). It resolves the Vercel OIDC token of a project. Not an official Vercel product.

```
dotnet add package Vercel.Oidc
```

`VercelOidcToken.GetVercelOidcTokenAsync` (upstream's `getVercelOidcToken`) returns the Vercel OIDC token. It reads the `x-vercel-oidc-token` header of the request context, then the `VERCEL_OIDC_TOKEN` environment variable. When the token is missing or expired, it refreshes it with the file-backed credentials of the Vercel CLI (`vercel login`) for the project linked in `.vercel/project.json`, caches it, and publishes it in `VERCEL_OIDC_TOKEN`. A login kept only in the system keyring is not read.

```csharp
using Vercel.Oidc;

var token = await VercelOidcToken.GetVercelOidcTokenAsync(null, cancellationToken);
```

A host that serves Vercel requests supplies the request context (upstream's `getContext`) with `VercelRequestContext.Use`, for example from ASP.NET Core middleware. Its `x-vercel-oidc-token` header is the OIDC token.

```csharp
using (VercelRequestContext.Use(new Dictionary<string, string?> { ["x-vercel-oidc-token"] = token }))
{
    // work that reads the token
}
```

## Not ported

- Keyring-only Vercel CLI logins (newer `@vercel/oidc` versions delegate to the CLI for them).
- `index-browser.test.ts`: there is no browser export.

## Differences from upstream

- The token is published in `VERCEL_OIDC_TOKEN` process-wide, as in `@vercel/oidc` 3.2.0.
- A refresh lock serializes concurrent refreshes in one process (.NET-only).
- Keyring-only Vercel CLI logins are not supported.

## License

Apache-2.0. Derived from `@vercel/oidc` by Vercel, Inc.
