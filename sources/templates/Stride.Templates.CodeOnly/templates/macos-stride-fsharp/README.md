# MyTemplate

Code-only Stride game written in F# for macOS.

This project consumes Stride packages from GitHub Packages. The generated
`NuGet.config` already includes the feed URL, but GitHub Packages still needs a
one-time authenticated source entry on each machine:

```bash
dotnet nuget add source --username GITHUB_USERNAME --password GITHUB_PAT_WITH_READ_PACKAGES --store-password-in-clear-text --name gurdasnijor-stride "https://nuget.pkg.github.com/gurdasnijor/index.json"
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
