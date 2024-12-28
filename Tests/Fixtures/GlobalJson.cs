
using System.Text.Json;

namespace BlazorMixApps.Test.Fixtures;

internal class GlobalJson
{
    internal class SdkType
    {
        public required string Version { get; set; }
        public bool AllowPrerelease { get; set; }
        public string RollForward { get; set; } = "latestMinor";
    }

    public required SdkType Sdk { get; set; }

    internal void Save(string globaJsonPath)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(globaJsonPath, json);
    }
}
