# Releasing gh-copilot-byok

This project publishes the .NET global tool package `gh-copilot-byok` to both NuGet.org and GitHub Packages (NuGet registry).

## One-time setup

1. Ensure package metadata is correct in `dotnet/CopilotX/CopilotX.csproj`.
2. Ensure workflow permissions include `packages: write` (already configured).
3. Add a `NUGET_API_KEY` repository secret with an API key from [nuget.org](https://www.nuget.org/) that has push permissions for the `gh-copilot-byok` package.

## Release workflow

1. Merge changes into `main` using conventional commit semantics (`feat:`, `fix:`, and `!`/`BREAKING CHANGE`).
2. GitHub Actions workflow `Release Please` updates or opens a release PR with:
  - Calculated semantic version
  - Generated changelog/release notes
3. Merge the release PR.
4. Release Please creates a GitHub Release and tag (`v*`).
5. GitHub Actions workflow `Release .NET Tool` runs on `release.published` and will:
  - Build and test
  - Pack with package version derived from release tag
  - Push package to NuGet.org (public, no authentication required for install)
  - Push package to GitHub Packages NuGet feed
  - Upload `.nupkg` to that published GitHub Release

## Install after release

### From NuGet.org (recommended — no authentication required)

```powershell
dotnet tool install --global gh-copilot-byok
```

If already installed:

```powershell
dotnet tool update --global gh-copilot-byok
```

### From GitHub Packages (alternative)

GitHub Packages NuGet feed requires authentication.

1. Create a PAT with `read:packages` scope.
2. Add GitHub feed as a source:

```powershell
dotnet nuget add source "https://nuget.pkg.github.com/sujithq/index.json" `
  --name github-sujithq `
  --username <github-username> `
  --password <PAT> `
  --store-password-in-clear-text
```

3. Install the tool:

```powershell
dotnet tool install --global gh-copilot-byok --add-source "https://nuget.pkg.github.com/sujithq/index.json"
```

If already installed:

```powershell
dotnet tool update --global gh-copilot-byok --add-source "https://nuget.pkg.github.com/sujithq/index.json"
```

## Dry run checks (local)

```powershell
dotnet build dotnet/CopilotX/CopilotX.csproj -c Release
dotnet run --project dotnet/CopilotX.Tests/CopilotX.Tests.csproj -c Release
dotnet pack dotnet/CopilotX/CopilotX.csproj -c Release --no-build
```
