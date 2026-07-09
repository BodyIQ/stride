# MyTemplate

Code-only Stride game written in F# with an Android app head.

This project consumes Stride packages from GitHub Packages. The generated
`NuGet.config` already includes the feed URL, but GitHub Packages still needs a
one-time authenticated source entry on each machine:

```bash
dotnet nuget add source --username GITHUB_USERNAME --password GITHUB_PAT_WITH_READ_PACKAGES --store-password-in-clear-text --name gurdasnijor-stride "https://nuget.pkg.github.com/gurdasnijor/index.json"
```

Build the Android APK:

```bash
dotnet restore MyTemplate.Android/MyTemplate.Android.csproj
dotnet build MyTemplate.Android/MyTemplate.Android.csproj -c Debug
```

Install to a connected Android device:

```bash
adb install -r MyTemplate.Android/bin/Debug/net10.0-android/android-arm64/com.gurdasnijor.mytemplate-Signed.apk
```
