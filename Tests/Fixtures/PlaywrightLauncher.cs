using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace BlazorMixApps.Test.Fixtures;

internal class PlaywrightLauncher : IAsyncDisposable
{
    private IPlaywright? _Playwright;

    private IBrowser? _Browser;

    private IPage? _Page;

    private class TestOptions
    {
        public string Browser { get; set; } = "";

        public bool Headless { get; set; } = true;

        public bool SkipInstallBrowser { get; set; } = false;
    }

    private static readonly Lazy<TestOptions> _Options = new(() =>
    {
        var options = new TestOptions();
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables(prefix: "DOTNET_")
            .AddTestParameters()
            .Build();
        configuration.Bind(options);

        if (!options.SkipInstallBrowser)
        {
            Microsoft.Playwright.Program.Main(["install"]);
        }
        return options;
    });

    public async ValueTask<IPage> GetPageAsync()
    {
        this._Playwright ??= await Playwright.CreateAsync();
        this._Browser ??= await this.LaunchBrowserAsync(this._Playwright);
        this._Page ??= await this._Browser.NewPageAsync();
        return this._Page;
    }

    private Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright)
    {
        var browserType = _Options.Value.Browser.ToLower() switch
        {
            "firefox" => playwright.Firefox,
            "webkit" => playwright.Webkit,
            _ => playwright.Chromium
        };

        var channel = _Options.Value.Browser.ToLower() switch
        {
            "firefox" or "webkit" => "",
            _ => _Options.Value.Browser.ToLower()
        };

        return browserType.LaunchAsync(new()
        {
            Channel = channel,
            Headless = _Options.Value.Headless,
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (this._Browser != null) await this._Browser.DisposeAsync();
        this._Playwright?.Dispose();
    }
}
