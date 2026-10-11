# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## What this repository is

A sample plus a reusable MSBuild workaround that lets a Blazor **app** project (Server or WebAssembly) reference other Blazor **app** projects, not just Razor class libraries. Out of the box the SDK fails with errors such as `Conflicting assets with the same target path '_framework/blazor.boot.json'`, and scoped CSS bundles, `.razor.js` locations, and publish output also break.

The deliverable is [Build/Build.targets](Build/Build.targets) (plus `Build/CustomTasks/*.targets`), licensed under The Unlicense. Everything else exists to exercise it. Supported SDKs: 8.0.x, 9.0.x, 10.0.x (the `net11` branch is adding 11).

## Commands

```sh
# Build the sample apps (each main app imports Build/Build.targets)
dotnet build BlazorMixApps.slnx

# Run a sample
dotnet run --project MainServerApp
dotnet run --project MainWasmApp

# Run all tests (same as CI; requires .NET SDKs 8, 9 and 10 installed side by side)
dotnet test --project ./Tests/BlazorMixApps.Test.csproj --settings ./.runsettings

# Run a subset (Microsoft.Testing.Platform + NUnit runner)
dotnet test --project ./Tests/BlazorMixApps.Test.csproj --settings ./.runsettings --filter "FullyQualifiedName~Publish_Test"
```

- `global.json` sets the test runner to `Microsoft.Testing.Platform`, so `dotnet test` needs `--project`.
- `.runsettings` forces one NUnit worker and `StopOnError`. `Tests/.settings/*.runsettings` choose the Playwright browser and headless mode instead (`Browser`, `Headless`, `SkipInstallBrowser` test parameters, also readable from `DOTNET_*` env vars).
- Tests write `.binlog` files for every build/publish into `binlogs/` at the repo root. Inspect these first when a test fails.
- `MainWPFApp` (BlazorWebView) is Windows-only and is not covered by the tests.

## Architecture

### Project layout

- `MainServerApp` references `WasmApp0`, `ServerApp1`, `RazorLib1`.
- `MainWasmApp` references `WasmApp0`, `WasmApp1`.
- `Referenced/*` are ordinary Blazor app projects (plus one RCL) that the main apps consume.
- Only projects that consume app projects import `Build/Build.targets`.

### How Build.targets works

1. `_BS_FilterAppProjectItems` picks out referenced projects whose `AdditionalPropertiesFromProject` metadata has `_IsExecutable=true`. Only these "app projects" are rewritten; RCL references pass through untouched.
2. `_BS_BundleAppProjectsScopedCss` synthesizes a `{App}.bundle.scp.css` StaticWebAsset and its endpoints (plain and fingerprinted) from each referenced app's `{App}.styles.css`, so the main app's `{Main}.styles.css` `@import`s them.
3. `_BS_FilterAndRebaseItems` (StaticWebAsset items) and `_BS_FilterAndRebaseStaticWebAssetEndPoints` (endpoint items) drop referenced apps' `_framework/*`, `*.styles.css` (and `.gz`/`.br`), and `appsettings*.json`, then rebase the rest under `_content/{App}/`. **Exception: `.js` assets keep a root base path**, so `js/helper.js` and `Components/Component0.razor.js` are served from `/`. Compressed assets whose `RelatedAsset` was pruned are also removed.
4. Targets are hooked into specific SDK targets for both build (`ResolveReferencedProjectsStaticWebAssets`, `ResolveBuildCompressedStaticWebAssets`, `GetCopyToOutputDirectoryItems`) and publish (`ComputeReferencedProjectsPublishAssets`, `ResolvePublishCompressedStaticWebAssets`, `GetCopyToPublishDirectoryItems`). `ComputeReferencedProjectsPublishAssets` loses endpoint items, so they are saved beforehand and re-added afterwards. `_FrameworkStaticWebAsset` items are passed in during publish so legitimate framework assets survive pruning.

The custom tasks are inline C# via `RoslynCodeTaskFactory`, so they must compile against whatever MSBuild ships with each supported SDK. Changes to the SDK's static web assets pipeline (new metadata, renamed targets, endpoint format) are the usual source of breakage on new .NET versions.

### Tests

`Tests/BuildAnRunTests.cs` is an end-to-end matrix over (target framework, SDK version, main project). For each case it:

1. Copies the repo into a temp dir with `Toolbelt.WorkDirectory` (skipping dot-folders, `Tests`, `bin`, `obj`, `dist`, `binlogs`) and writes a `global.json` pinning the SDK major.
2. Rewrites `<TargetFramework>` in the main project and `WasmApp0`, and swaps version-specific files: `wwwroot/index.net10.html` replaces `index.html` for net10+, and `Pages/_Host.net9.cshtml` replaces `_Host.cshtml` for net9+. Keep these variants in sync when changing the host pages.
3. Builds and runs, or publishes and runs (`dotnet exec` for Server, the local `dotnet-serve` tool for Wasm), then checks over HTTP and Playwright that the scoped CSS imports, `_content/WasmApp0/assets/bg.png`, the root-relative `.js` files, the button styling, and the JS `prompt` interop all work.

Adding a new SDK/TFM means adding rows to `TestCases`, `PackageReference` groups in the Wasm `.csproj` files, and the SDK to `.github/workflows/unit-tests.yml`.
