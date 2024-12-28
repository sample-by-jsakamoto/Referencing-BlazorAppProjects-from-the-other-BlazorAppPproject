using Microsoft.JSInterop;

namespace ServerApp0;

public class Helper
{
    private readonly IJSRuntime _JSRuntime;

    public Helper(IJSRuntime jsRuntime)
    {
        this._JSRuntime = jsRuntime;
    }

    public async Task<string?> PromptAsync(string? message)
    {
        await using var module = await this._JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/helper.js");
        return await module.InvokeAsync<string>("prompt", message);
    }
}
