# GitHub Packages Feed

This fork can publish the full compatible Stride package set to GitHub Packages from a manually dispatched workflow.

## Quick workflow

The fork package feed contains the macOS/F# code-only template and its matching engine/toolkit packages. Use one exact package version for the template and generated project packages when the packages come from the GitHub Actions publish workflow.

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

The workflow builds and publishes the template and the engine/toolkit packages with the same version. Avoid publishing a template-only package with a different version unless you also intentionally pin the generated project to an already-published engine/toolkit version.

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

For projects that use both nuget.org and this fork, package source mapping avoids accidental restores from the wrong feed:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="gurdasnijor-stride" value="https://nuget.pkg.github.com/gurdasnijor/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
      <package pattern="Stride.Dependencies.*" />
      <package pattern="Stride.GNU.*" />
      <package pattern="Stride.Mono.*" />
      <package pattern="Stride.GraphX.*" />
      <package pattern="Stride.Metrics" />
      <package pattern="Stride.QuickGraph" />
    </packageSource>
    <packageSource key="gurdasnijor-stride">
      <package pattern="Stride" />
      <package pattern="Stride.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

Then pin generated projects to the published version:

```xml
<PackageReference Include="Stride.CommunityToolkit.CodeOnly" Version="4.4.0-github-123.1" />
```

The code-only template package is published to the same feed. Install the exact version you want to test:

```bash
dotnet new install Stride.Templates.CodeOnly@4.4.0-github-123.1
dotnet new stride-macos-fsharp -n MyFSharpGame
```

The generated project will already reference `Stride.CommunityToolkit.CodeOnly` at that template's engine version.

The generated project also includes a `NuGet.config` with nuget.org and this fork's GitHub Packages feed. It does not include credentials; authenticate the `gurdasnijor-stride` source once per consuming machine.

To verify the packaged native runtime after creating a project:

```bash
dotnet publish -c Release
./bin/Release/net10.0/$(dotnet --info | awk '/RID:/ { print $2; exit }')/publish/MyFSharpGame
```
