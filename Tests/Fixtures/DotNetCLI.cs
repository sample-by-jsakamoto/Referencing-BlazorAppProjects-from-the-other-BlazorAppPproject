using Toolbelt.Diagnostics;

namespace BlazorMixApps.Test.Fixtures;

/// <summary>
/// Provides methods to launch the dotnet CLI as a child process.
/// </summary>
internal static class DotNetCLI
{
    // "dotnet test" can leak these to the test process, which pins child builds to the parent's SDK regardless of global.json.
    private static readonly string[] _VariablesToClear = ["MSBuildSDKsPath", "MSBuildExtensionsPath", "MSBUILD_EXE_PATH", "DOTNET_HOST_PATH"];

    /// <summary>
    /// Starts the dotnet CLI with the specified arguments, without inheriting the MSBuild-related environment variables of the current process.
    /// </summary>
    /// <param name="arguments">The command line arguments to pass to the dotnet CLI.</param>
    /// <param name="workingDirectory">The working directory for the dotnet CLI process.</param>
    /// <returns>The started process.</returns>
    public static XProcess Start(string arguments, string workingDirectory)
    {
        return XProcess.Start("dotnet", arguments, workingDirectory, options =>
        {
            // A null value removes the variable from the child process environment.
            foreach (var name in _VariablesToClear) options.EnvironmentVariables[name] = null!;
        });
    }
}
