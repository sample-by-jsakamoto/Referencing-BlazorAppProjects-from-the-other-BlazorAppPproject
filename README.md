# Referencing Blazor app projects (not Razor class library projects) from the other Blazor app project

## Introduction

When you add project references from a Blazor app project to another Blazor app project, you will see build errors like those below.

```
Microsoft.NET.Sdk.StaticWebAssets.targets(475,5):
error : Conflicting assets with the same target path '_framework/blazor.boot.json'.
For assets 'Identity: ...\blazor.boot.json' and 'Identity: ...\blazor.boot.json' from different projects.
```

This is because Blazor app projects are not designed to be referenced from another Blazor app project. In fact, there are more problems, not only with the above error but also with other issues, such as bundling scoped CSS files, location of .razor.js static web assets, and so on.

## Solution

Fortunately, you can work around this limitation by importing [the MSBuild script in this repository](Build/Build.targets), like below.

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  ...
  <Import Project="..\Build\Build.targets" />
</Project>
```

That MSBuild script is designed to be imported into the Blazor app project, which references other Blazor app projects. That MSBuild script includes hacks to avoid the abovementioned problems when a Blazor app project references other Blazor app projects.

You can try this solution by cloning this repository and running the `MainServerApp`, `MainWasmApp`, and `MainWPFApp` projects.

## License

The MSBuild script (.targets file) in this repository is licensed under [The Unlicense](LICENSE).
