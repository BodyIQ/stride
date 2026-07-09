# GitHub Packages Feed

This fork can publish the compatible Stride engine package set to GitHub Packages from a manually dispatched workflow.

## Quick workflow

The fork package feed contains the macOS/F# code-only template and its matching engine packages. Generated projects use upstream `Stride.CommunityToolkit` packages from nuget.org and pin `Stride.*` engine packages to the same fork version as the template.

One-time setup on a consuming machine:

```bash
dotnet nuget add source \
  --username GITHUB_USERNAME \
  --password GITHUB_PAT_WITH_READ_PACKAGES \
  --store-password-in-clear-text \
  --name gurdasnijor-stride \
  "https://nuget.pkg.github.com/gurdasnijor/index.json"
```

Create a new F# macOS Stride game:

```bash
dotnet new install Stride.Templates.CodeOnly@PACKAGE_VERSION
dotnet new stride-macos-fsharp -n MyFSharpGame
cd MyFSharpGame
dotnet run
```

If that exact template version is already installed, reinstall it with `--force`:

```bash
dotnet new install Stride.Templates.CodeOnly@PACKAGE_VERSION --force
```

For an in-repo local development loop that does not go through GitHub Packages:

```bash
cd /path/to/stride
dotnet build sources/templates/Stride.Templates.CodeOnly/Stride.Templates.CodeOnly.csproj -p:StrideInstallTemplate=true
dotnet new stride-macos-fsharp -n MyFSharpGame
```

## Publish

Run **Publish GitHub Packages** from GitHub Actions on the branch you want to test.

By default, the workflow publishes a unique prerelease version:

```text
4.4.0-github-<run>.<attempt>
```

You can also pass a custom prerelease suffix, without the leading dash. Do not reuse a suffix unless you intentionally want `--skip-duplicate` to keep the already-published package version.

The workflow builds and publishes the template and the engine packages with the same version. Avoid publishing a template-only package with a different version unless you also intentionally pin the generated project to an already-published engine version.

The workflow uses the repository `GITHUB_TOKEN` with `packages: write`; no personal access token is needed for publishing from Actions.

If the publish step returns `403 Forbidden` for existing package IDs, grant this repository write access to those packages in GitHub Packages settings, then rerun. The workflow fails on any package push failure so missing package IDs cannot be silently ignored.

## Consume

GitHub Packages requires authentication for restore. Add the package source with a personal access token classic that has at least `read:packages`:

```bash
dotnet nuget add source \
  --username GITHUB_USERNAME \
  --password GITHUB_PAT \
  --store-password-in-clear-text \
  --name gurdasnijor-stride \
  "https://nuget.pkg.github.com/gurdasnijor/index.json"
```

Generated projects use a simple two-source `NuGet.config`: nuget.org for upstream dependencies such as `Stride.CommunityToolkit`, and this fork's GitHub Packages feed for the pinned `Stride.*` engine package versions:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="gurdasnijor-stride" value="https://nuget.pkg.github.com/gurdasnijor/index.json" />
  </packageSources>
</configuration>
```

If you are creating a project manually instead of using the template, reference upstream toolkit packages and pin the required engine packages to the fork version:

```xml
<PackageReference Include="Stride.CommunityToolkit" Version="1.0.0-preview.62" />
<PackageReference Include="Stride.CommunityToolkit.Skyboxes" Version="1.0.0-preview.62" />
<PackageReference Include="Stride.Engine" Version="4.4.0-github-123.1" />
<PackageReference Include="Stride.Particles" Version="4.4.0-github-123.1" />
<PackageReference Include="Stride.Physics" Version="4.4.0-github-123.1" />
<PackageReference Include="Stride.UI" Version="4.4.0-github-123.1" />
```

The code-only template package is published to the same feed. Install the exact version you want to test:

```bash
dotnet new install Stride.Templates.CodeOnly@4.4.0-github-123.1
dotnet new stride-macos-fsharp -n MyFSharpGame
```

The generated project will already reference the upstream toolkit packages and pin the Stride engine packages to that template's engine version.

The generated project also includes a `NuGet.config` with nuget.org and this fork's GitHub Packages feed. It does not include credentials; authenticate the `gurdasnijor-stride` source once per consuming machine.

To verify the packaged native runtime after creating a project:

```bash
dotnet publish -c Release
./bin/Release/net10.0/$(dotnet --info | awk '/RID:/ { print $2; exit }')/publish/MyFSharpGame
```
