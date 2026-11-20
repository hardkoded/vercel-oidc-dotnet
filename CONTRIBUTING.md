# Contributing

```
dotnet tool restore
dotnet build
dotnet test
dotnet csharpier format .
```

- `src/Vercel.Oidc` is the library, packed as `Vercel.Oidc`.
- `tests/Vercel.Oidc.Tests` holds the tests. Tests ported from `@vercel/oidc` keep upstream's names. `VercelRequestContextTests` is .NET-only.
- Releases: push a version tag such as `v0.1.0` (previews use `v0.1.0-preview.1`). `.github/workflows/publish.yml` runs the tests, packs, and pushes to nuget.org with trusted publishing. The workflow file has to be on the default branch before the tag is pushed.
