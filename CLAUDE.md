# CLAUDE.md

Community .NET port of `@vercel/oidc` (version 3.2.0, the one `@ai-sdk/gateway` pins). The `Vercel.Oidc` NuGet package resolves the Vercel OIDC token of a project. `hardkoded/e2e-dotnet` consumes it for its Vercel AI Gateway decision model.

## Target frameworks

- `src/Vercel.Oidc` targets `net10.0` and `netstandard2.0`. Code must compile on both. Guard net10-only APIs with `#if NET`.
- Tests target `net10.0` only.

## Porting

- Upstream is `@vercel/oidc` (https://github.com/vercel/vercel, `packages/oidc`). Read the published 3.2.0 package (`npm pack @vercel/oidc@3.2.0`) before you change behavior. Reuse its names and files in C# casing.
- If upstream behaves the same on purpose, do not "fix" it here. Report it, and open an issue for a hardening change.
- A name upstream does not have is .NET-only. Name it in the PR report with the reason.
- Port tests from upstream's `*.test.ts`, with the same names. Upstream mocks the `token-util` functions and `fs`. The port uses real files in a temporary directory and a fake Vercel API behind the HTTP client (`FakeVercelApi`), and checks the same calls on the requests and files. Only `getTokenPayload` has a mock seam (`TokenUtil.GetTokenPayloadOverride`).
- Keep the file header (`Copyright 2023 Vercel, Inc.` and `SPDX-License-Identifier: Apache-2.0`). The `.editorconfig` enforces it.

## Style and tools

- CSharpier owns formatting. Run `dotnet csharpier format .` before every commit. The Husky.Net pre-push hook runs `dotnet csharpier check .`. Do not run `dotnet format whitespace`.
- The build enforces the `.editorconfig` and treats warnings as errors.
- This environment may lack the SDK in `global.json`. Roll it forward with a local, uncommitted `global.json` change if needed.

## Releases

- The version comes from the git tag through MinVer. Push `v0.1.0` (previews use `v0.1.0-preview.1`).
- `.github/workflows/publish.yml` packs `Vercel.Oidc` and pushes it to nuget.org with trusted publishing. The policy is owner `hardkoded`, repository `vercel-oidc-dotnet`, workflow file `publish.yml`, no environment. Do not rename the file and do not add an `environment:` to the job.
