using System.Text.Json;

namespace Studue.Services;

public class BuildingIndex
{
    private readonly Dictionary<string, string> _campusByCode = new(StringComparer.OrdinalIgnoreCase);

    public string Json { get; } = "{}";

    public BuildingIndex(IWebHostEnvironment environment, ILogger<BuildingIndex> logger)
    {
        var path = Path.Combine(environment.WebRootPath, "maps", "buildings.json");

        if (!File.Exists(path))
        {
            logger.LogWarning("No building index at {path}; the map will fall back to fetching it", path);
            return;
        }

        var json = File.ReadAllText(path);

        using var document = JsonDocument.Parse(json);
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            if (entry.Value.TryGetProperty("campus", out var campus) && campus.GetString() is { } id)
                _campusByCode[entry.Name] = id;
        }

        // inlined into a <script> tag, where a "</script>" inside any name would end it early.
        // "<" only ever appears inside JSON strings, so escaping it keeps the document valid.
        Json = json.Replace("<", "\\u003c");
    }

    public string? CampusFor(string? code) =>
        code != null && _campusByCode.TryGetValue(code, out var campus) ? campus : null;

    public IReadOnlyCollection<string> CampusIds => _campusByCode.Values.Distinct().ToList();

    public string? DefaultCampus => _campusByCode
        .OrderBy(x => x.Key, StringComparer.Ordinal)
        .Select(x => x.Value)
        .FirstOrDefault();
}
