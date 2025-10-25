using Microsoft.Playwright;

namespace BlazorMixApps.Test.Fixtures;

internal static class PlaywrightExtensions
{
    public static async ValueTask GotoAndWaitForReadyAsync(this IPage page, string url)
    {
        var waiter = page.WaitForBlazorHasBeenStarted();
        await page.GotoAsync(url);
        await waiter;
        await Task.Delay(200);
    }

    public static async ValueTask WaitForAsync(this IPage page, Func<IPage, ValueTask<bool>> predictAsync, bool throwOnTimeout = true)
    {
        var canceller = new CancellationTokenSource(millisecondsDelay: 5000);
        do
        {
            if (await predictAsync(page)) return;
            await Task.Delay(100);
        } while (!canceller.IsCancellationRequested);
        if (throwOnTimeout) throw new OperationCanceledException(canceller.Token);
    }

    public static async ValueTask WaitForBlazorHasBeenStarted(this IPage page)
    {
        await page.WaitForConsoleMessageAsync(new() { Predicate = message => message.Text == "Blazor has been started." });
    }

    public static async ValueTask AssertEqualsAsync<T>(this IPage page, Func<IPage, Task<T>> selector, T expectedValue)
    {
        var actualValue = default(T);
        await page.WaitForAsync(async p =>
        {
            actualValue = await selector.Invoke(p);
            return actualValue!.Equals(expectedValue);
        }, throwOnTimeout: false);
        actualValue.Is(expectedValue);
    }

    public static async ValueTask AssertEqualsAsync<T>(this IPage page, Func<IPage, Task<IEnumerable<T>>> selector, IEnumerable<T> expectedValue)
    {
        var actualValue = Enumerable.Empty<T>();
        await page.WaitForAsync(async p =>
        {
            actualValue = await selector.Invoke(p);
            return Enumerable.SequenceEqual(actualValue, expectedValue);
        }, throwOnTimeout: false);
        actualValue.Is(expectedValue);
    }

    public static async ValueTask AssertUrlIsAsync(this IPage page, string expectedUrl)
    {
        expectedUrl = expectedUrl.TrimEnd('/');
        await page.AssertEqualsAsync(async _ =>
        {
            var href = await _.EvaluateAsync<string>("window.location.href");
            return href.TrimEnd('/');
        }, expectedUrl);
    }
    
    public static Task<string> CSSValueAsync(this ILocator locator, string cssProperty)
    {
        return locator.EvaluateAsync<string>($"element => window.getComputedStyle(element).{cssProperty}");
    }

    public static Task<bool> IsContentsSelectedAsync(this IPage page)
    {
        return page.EvaluateAsync<bool>("getSelection().type === 'Range'");
    }

    /// <summary>
    /// This is an alternative to the <see cref="IPage.FocusAsync(string)"/> method, which does not work well in custom elements cases.<br/>
    /// This method doesn't use Playwright's <see cref="IPage.FocusAsync(string)"/> method, but uses JavaScript to focus on the element.
    /// </summary>
    public static async ValueTask FocusByScriptAsync(this IPage page, string selector)
    {
        await page.WaitForSelectorAsync(selector);
        await page.EvaluateAsync($"document.querySelector(\"{selector}\").focus()");
    }
}
