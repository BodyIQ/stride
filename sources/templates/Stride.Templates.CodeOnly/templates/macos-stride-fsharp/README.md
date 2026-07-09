# MyTemplate

Code-only Stride game written in F# for macOS.

This project consumes upstream `Stride.CommunityToolkit` packages from nuget.org
and pinned Stride engine packages from GitHub Packages. The generated
`NuGet.config` already includes the feed URL, but GitHub Packages still needs a
one-time authenticated source entry on each machine:

```bash
dotnet nuget add source --username GITHUB_USERNAME --password GITHUB_PAT_WITH_READ_PACKAGES --store-password-in-clear-text --name bodyiq-stride "https://nuget.pkg.github.com/BodyIQ/index.json"
```

```bash
dotnet restore
dotnet run
```

To verify the packaged native runtime output:

```bash
dotnet publish -c Release
./bin/Release/net10.0/$(dotnet --info | awk '/RID:/ { print $2; exit }')/publish/MyTemplate
```
