using System.Text.RegularExpressions;
using System.Xml.Linq;
using BlazorMixApps.Test.Fixtures;
using Toolbelt;
using static Toolbelt.Diagnostics.XProcess;

namespace BlazorMixApps.Test;

[Parallelizable(ParallelScope.Children)]
public class BuildAnRunTests
{
    private readonly HttpClient _httpClient = new();

    private static IEnumerable<object[]> TestCases { get; } = [
    // targetFrameworkVer, sdkVersion, mainProject, referencedProjects[]
    [8, 8, "MainServerApp", new[] { "RazorLib1", "WasmApp0", "ServerApp1" }],
    [8, 9, "MainServerApp", new[] { "RazorLib1", "WasmApp0", "ServerApp1" }],
    [9, 9, "MainServerApp", new[] { "RazorLib1", "WasmApp0", "ServerApp1" }],
    [8, 8, "MainWasmApp", new[] { "RazorLib1", "WasmApp0", "WasmApp1" }],
    [8, 9, "MainWasmApp", new[] { "RazorLib1", "WasmApp0", "WasmApp1" }],
    [9, 9, "MainWasmApp", new[] { "RazorLib1", "WasmApp0", "WasmApp1" }],
    ];

    private string GetBinLogPath(int sdkVersion, string actionName, string projectName)
    {
        var slnDir = FileIO.FindContainerDirToAncestor("BlazorMixApps.Tests.sln");
        var binlogsDir = Path.Combine(slnDir, "binlogs");
        Directory.CreateDirectory(binlogsDir);
        return Path.Combine(binlogsDir, $"{DateTime.Now:yyyy-MM-dd-HHmmss.fff}-sdk-{sdkVersion}-{actionName}-{projectName}.binlog");
    }

    /// <summary>
    /// Copy projects to temp directory and set SDK version in global.json.
    /// </summary>
    /// <param name="sdkVersion">Specify the .NET SDK version to use in this work space. <br/>This method will create a global.json file in the work space dir to specify the SDK version.</param>
    private async ValueTask<WorkDirectory> PrepareWorkspaceAsync(int targetFrameworkVer, int sdkVersion, string mainProject)
    {
        // GIVEN: Copy projects to temporary working directory
        var slnDir = FileIO.FindContainerDirToAncestor("BlazorMixApps.Tests.sln");
        var workDir = WorkDirectory.CreateCopyFrom(slnDir, path => !(path.Name.StartsWith(".") || "dist;Tests;bin;obj;work;binlogs".Split(';').Contains(path.Name)));

        // GIVEN: Create global.json to specify the SDK version
        var globalJson = new GlobalJson { Sdk = new() { Version = $"{sdkVersion}.0.0" } };
        globalJson.Save(Path.Combine(workDir, "global.json"));

        // GIVEN: Verify the SDK version
        using var dotnetVersion = await Start("dotnet", "--version", workDir).WaitForExitAsync();
        dotnetVersion.ExitCode.Is(0);
        dotnetVersion.Output.StartsWith($"{sdkVersion}.").IsTrue();

        // GIVEN: Rewrite the <TargetFramework> in the main project file
        var projectFilePath = Path.Combine(workDir, mainProject, $"{mainProject}.csproj");
        var projectFile = XDocument.Load(projectFilePath);
        var targetFrameworkNode = projectFile.Root?.Element("PropertyGroup")?.Element("TargetFramework") ?? throw new Exception("The project file is not valid.");
        targetFrameworkNode.Value = $"net{targetFrameworkVer}.0";
        projectFile.Save(projectFilePath);

        return workDir;
    }

    [TestCaseSource(nameof(TestCases))]
    public async Task Build_and_Run_Test(int targetFrameworkVer, int sdkVersion, string mainProject, string[] referencedProjects)
    {
        // GIVEN: Copy projects to temp directory, set SDK version in global.json, and rewrite the <TargetFramework> in the main project file.
        using var workDir = await this.PrepareWorkspaceAsync(targetFrameworkVer, sdkVersion, mainProject);

        // WHEN: Build the project,
        var projectDir = Path.Combine(workDir, mainProject);
        var binLog = this.GetBinLogPath(sdkVersion, "build", mainProject);
        using var dotnetBuild = await Start("dotnet", $"build -f net{targetFrameworkVer}.0 -bl:\"{binLog}\"", projectDir).WaitForExitAsync();

        // THEN: and it should succeed
        dotnetBuild.ExitCode.Is(0, message: dotnetBuild.Output);

        // WHEN: Run the project,
        var url = $"http://localhost:{TcpNetwork.GetFreeTcpPortNumber()}";
        using var dotnetRun = Start("dotnet", $"run -f net{targetFrameworkVer}.0 --no-build --urls {url}", projectDir);
        var success = await dotnetRun.WaitForOutputAsync(output => output.Contains("Application started"), millsecondsTimeout: 5000);
        success.IsTrue(message: dotnetRun.Output);

        // THEN: and it should serve static web assets including other referenced projects' assets
        await this.Verify(mainProject, referencedProjects, url);
    }

    [TestCaseSource(nameof(TestCases))]
    public async Task Publish_Test(int targetFrameworkVer, int sdkVersion, string mainProject, string[] referencedProjects)
    {
        // GIVEN: Copy projects to temp directory, set SDK version in global.json, and rewrite the <TargetFramework> in the main project file.
        using var workDir = await this.PrepareWorkspaceAsync(targetFrameworkVer, sdkVersion, mainProject);

        // WHEN: Publish the project,
        var projectDir = Path.Combine(workDir, mainProject);
        var distDir = Path.Combine(workDir, "dist");
        var binLog = this.GetBinLogPath(sdkVersion, "publish", mainProject);
        using var dotnetPublish = await Start("dotnet", $"publish -c Release -f net{targetFrameworkVer}.0 -o \"{distDir}\" -bl:\"{binLog}\"", projectDir).WaitForExitAsync();

        // THEN: and it should succeed
        dotnetPublish.ExitCode.Is(0, message: dotnetPublish.Output);

        // WHEN: Run the published app,
        var tcpPort = TcpNetwork.GetFreeTcpPortNumber();
        var url = $"http://localhost:{tcpPort}";
        var wwwrootDir = Path.Combine(distDir, "wwwroot");
        var testProjDir = FileIO.FindContainerDirToAncestor("BlazorMixApps.Test.csproj");
        (await Start("dotnet", "tool restore", testProjDir).WaitForExitAsync()).Dispose();

        using var dotnetExec =
            File.Exists(Path.Combine(distDir, $"{mainProject}.dll")) ? Start("dotnet", $"exec \"{mainProject}.dll\" --urls {url}", distDir) :
            Directory.Exists(wwwrootDir) ? Start("dotnet", $"serve -d:\"{wwwrootDir}\" -p:{tcpPort} --default-extensions:html", testProjDir)
            : throw new Exception("The published app is not tested yet.");

        var success = await dotnetExec.WaitForOutputAsync(output => output.Contains("Press CTRL+C to ", StringComparison.InvariantCultureIgnoreCase), millsecondsTimeout: 5000);
        success.IsTrue(message: dotnetExec.Output);

        // THEN: and it should serve static web assets including other referenced projects' assets
        await this.Verify(mainProject, referencedProjects, url);
    }

    private async Task Verify(string mainProject, string[] referencedProjects, string url)
    {
        // The "<Main Project>.styles.css" should be exist
        var appStylesCss = await this._httpClient.GetStringAsync($"{url}/{mainProject}.styles.css");

        // It should import all referenced projects' bundle css
        var imports = referencedProjects
            .Select(project => Regex.Match(appStylesCss, $@"@import '(?<path>_content/{project}/{project}(\.[a-z0-9]+)?\.bundle\.scp\.css)';"))
            .Select(match => match.Groups["path"])
            .Select(group => group.Success ? group.Value : null)
            .ToArray();
        imports.Where(path => path is not null).Count().Is(referencedProjects.Length);

        // The imported bundle css should be fetched successfully
        var fetchBundleCss = imports.Select(path => this._httpClient.GetStringAsync($"{url}/{path}")).ToArray();
        await Task.WhenAll(fetchBundleCss);
        fetchBundleCss.All(t => t.IsCompletedSuccessfully).IsTrue();

        // Other assets under the referenced projects should be fetched successfully
        var bgAsset = await this._httpClient.GetAsync($"{url}/_content/{"WasmApp0"}/assets/bg.png");
        bgAsset.IsSuccessStatusCode.IsTrue();

        // Special case: JavaScript files under the referenced projects should be fetched from root url successfully
        var helperJs = await this._httpClient.GetStringAsync($"{url}/js/helper.js");
        helperJs.Is("export const prompt = (message) => window.prompt(message);");

        var razorJs = await this._httpClient.GetStringAsync($"{url}/Components/Component0.razor.js");
        razorJs.Is("export const showMessage = (message) => alert(message); ");
    }
}
