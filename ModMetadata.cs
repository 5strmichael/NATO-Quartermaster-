using SPTarkov.Server.Core.Models.Spt.Mod;

namespace NATOQuartermaster;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.michael.spt.natoquartermaster";
    public string Name { get; init; } = "NATO Quartermaster";
    public string Author { get; init; } = "Michael";
    public List<string>? Contributors { get; init; } = ["OpenAI ChatGPT"];
    public SemanticVersioning.Version Version { get; init; } = new("1.1.1");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.3");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/5strmichael/NATO-Quartermaster-";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}
