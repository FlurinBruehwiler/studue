// Generates the campus map data used by /map, from OpenStreetMap.
//
// Run offline from the repository root; the output is committed, and nothing
// queries Overpass at runtime.
//
//   dotnet run --project tools/MapGenerator            all campuses
//   dotnet run --project tools/MapGenerator -- TS      only the campus containing TS
//
// Output (coordinates are [lat, lon], the order Leaflet expects):
//   Studue/wwwroot/maps/buildings.json   code -> campus, centre, name
//   Studue/wwwroot/maps/<campus>.json    one campus's geometry
//
// Map data (c) OpenStreetMap contributors, ODbL.

using System.Globalization;
using System.Text.Json;

const string Overpass = "https://overpass-api.de/api/interpreter";
const string OutputDirectory = "Studue/wwwroot/maps";

// everything ZHAW, from Waedenswil up past Winterthur
const double SearchSouth = 47.15, SearchWest = 8.40, SearchNorth = 47.60, SearchEast = 8.85;
// The building code carries the campus: TS/TE/TH are Technikumstrasse, G* is Grüental,
// R* is Reidbach. Grouping by distance instead merged Grüental into Reidbach and split
// Reidbach in two, so the code is the authority and proximity is only a fallback.
var campusByCode = new Dictionary<string, string>
{
    ["ZF"] = "Campus Zentrum, Toni-Areal",
    ["ZA"] = "Campus Zentrum, Lagerstrasse",
    ["ZL"] = "Campus Zentrum, Lagerstrasse",
};

// Hand-verified addresses. These win over whatever OSM has named: an entry here is
// used both to add a building OSM does not label and to correct one it labels wrongly.
var buildingsByAddress = new Dictionary<string, (string Street, string Number, double South, double West, double North, double East)>
{
    ["ZA"] = ("Militärstrasse", "48", 47.36, 8.50, 47.40, 8.56),
    ["ZL"] = ("Lagerstrasse", "41", 47.36, 8.50, 47.40, 8.56),
};

var campusByPrefix = new Dictionary<char, string>
{
    ['T'] = "Campus Technikumstrasse",
    ['S'] = "Campus St.-Georgen-Platz",
    ['M'] = "Campus Stadt-Mitte",
    ['G'] = "Campus Grüental",
    ['R'] = "Campus Reidbach",
};

string CampusOf(Building building) =>
    campusByCode.TryGetValue(building.Code, out var byCode) ? byCode
    : campusByPrefix.TryGetValue(building.Code[0], out var byPrefix) ? byPrefix
    : $"ZHAW {building.Code}";

const double PaddingMetres = 260;        // context drawn around a campus

var roadClasses = new Dictionary<string, string>
{
    ["motorway"] = "major", ["trunk"] = "major", ["primary"] = "major", ["secondary"] = "major",
    ["tertiary"] = "minor", ["unclassified"] = "minor", ["residential"] = "minor",
    ["living_street"] = "minor", ["service"] = "minor",
    ["pedestrian"] = "path", ["footway"] = "path", ["path"] = "path", ["steps"] = "path", ["cycleway"] = "path",
};

var wanted = args.Length > 0 ? args[0] : null;

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
http.DefaultRequestHeaders.Add("User-Agent", "studue-map-generator/1.0");

Directory.CreateDirectory(OutputDirectory);

Console.WriteLine("finding ZHAW buildings...");
var buildings = await FindZhawBuildings();
Console.WriteLine($"  {buildings.Count} buildings with a code");

var campuses = buildings
    .GroupBy(CampusOf)
    .OrderBy(x => x.Key, StringComparer.Ordinal)
    .Select(x => x.OrderBy(y => y.Code, StringComparer.Ordinal).ToList())
    .ToList();
Console.WriteLine($"  {campuses.Count} campuses");

var index = new SortedDictionary<string, object>();

foreach (var members in campuses)
{
    var name = CampusOf(members[0]);
    var id = Slug(name);
    var codes = string.Join(",", members.Select(x => x.Code));

    foreach (var member in members)
    {
        index[member.Code] = new { campus = id, campusName = name, lat = member.Lat, lon = member.Lon, name = member.Name };
    }

    if (wanted != null
        && !id.Contains(wanted, StringComparison.OrdinalIgnoreCase)
        && !members.Any(x => x.Code.Equals(wanted, StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine($"  skipping {id} ({codes})");
        continue;
    }

    Console.WriteLine($"  {id}: {codes}");
    var campus = await FetchCampus(id, name, members);

    var path = Path.Combine(OutputDirectory, $"{id}.json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(campus));
    Console.WriteLine($"    {campus.features.Count} features, {new FileInfo(path).Length / 1024} KB");

    await Task.Delay(TimeSpan.FromSeconds(4));
}

await File.WriteAllTextAsync(Path.Combine(OutputDirectory, "buildings.json"), JsonSerializer.Serialize(index));
Console.WriteLine($"wrote index with {index.Count} buildings");

async Task<JsonDocument> Query(string query)
{
    for (var attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
            var response = await http.PostAsync(Overpass, content);
            var body = await response.Content.ReadAsStringAsync();

            if (body.TrimStart().StartsWith('{'))
                return JsonDocument.Parse(body);
        }
        catch (Exception error)
        {
            Console.WriteLine($"    attempt {attempt} failed: {error.Message}");
        }

        var wait = 10 * attempt;
        Console.WriteLine($"    overpass busy, retrying in {wait}s");
        await Task.Delay(TimeSpan.FromSeconds(wait));
    }

    throw new Exception("overpass did not answer; try again later");
}

async Task<List<Building>> FindZhawBuildings()
{
    var box = $"{F(SearchSouth)},{F(SearchWest)},{F(SearchNorth)},{F(SearchEast)}";

    using var document = await Query(
        "[out:json][timeout:90];(" +
        $"way[\"building\"][\"name\"~\"ZHAW\",i]({box});" +
        $"relation[\"building\"][\"name\"~\"ZHAW\",i]({box});" +
        ");out tags center;");

    var found = new Dictionary<string, Building>();

    foreach (var element in document.RootElement.GetProperty("elements").EnumerateArray())
    {
        if (!element.TryGetProperty("tags", out var tags) || !tags.TryGetProperty("name", out var nameValue))
            continue;
        if (!element.TryGetProperty("center", out var centre))
            continue;

        var name = nameValue.GetString() ?? "";
        var code = name.Replace('-', ' ').Split(' ').Skip(1)
            .FirstOrDefault(x => x.Length == 2 && x.All(char.IsAsciiLetterUpper));

        if (code == null || found.ContainsKey(code))
            continue;

        found[code] = new Building(code, name, Tag(tags, "addr:street"), Tag(tags, "addr:housenumber"),
            centre.GetProperty("lat").GetDouble(),
            centre.GetProperty("lon").GetDouble());
    }

    foreach (var (code, address) in buildingsByAddress)
    {
        var addressBox = $"{F(address.South)},{F(address.West)},{F(address.North)},{F(address.East)}";
        using var located = await Query(
            "[out:json][timeout:60];(" +
            $"way[\"building\"][\"addr:street\"=\"{address.Street}\"][\"addr:housenumber\"~\"^{address.Number}\"]({addressBox});" +
            ");out tags center;");

        var first = located.RootElement.GetProperty("elements").EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object)
        {
            Console.WriteLine($"  {code}: no building at {address.Street} {address.Number}");
            continue;
        }

        var centre = first.GetProperty("center");
        found[code] = new Building(code, $"ZHAW {code}", address.Street, address.Number,
            centre.GetProperty("lat").GetDouble(), centre.GetProperty("lon").GetDouble());
    }

    return found.Values.ToList();
}

async Task<Campus> FetchCampus(string id, string name, List<Building> members)
{
    var padLat = PaddingMetres / 111_320;
    var padLon = PaddingMetres / (111_320 * Math.Cos(double.DegreesToRadians(members.Average(x => x.Lat))));

    var south = members.Min(x => x.Lat) - padLat;
    var north = members.Max(x => x.Lat) + padLat;
    var west = members.Min(x => x.Lon) - padLon;
    var east = members.Max(x => x.Lon) + padLon;
    var box = $"{F(south)},{F(west)},{F(north)},{F(east)}";

    using var document = await Query(
        "[out:json][timeout:120];(" +
        $"way[\"building\"]({box});" +
        $"way[\"highway\"]({box});" +
        $"way[\"waterway\"=\"river\"]({box});" +
        $"way[\"waterway\"=\"stream\"]({box});" +
        $"way[\"natural\"=\"water\"]({box});" +
        ");out geom;");

    //a code with a hand-verified address is matched on that alone, never on the OSM name
    var corrected = buildingsByAddress.Keys.ToHashSet();

    var codesByName = members
        .Where(x => !corrected.Contains(x.Code))
        .ToDictionary(x => x.Name, x => x.Code);

    var codesByAddress = members
        .Where(x => corrected.Contains(x.Code) && x.Street != null && x.Number != null)
        .ToDictionary(x => $"{x.Street}|{x.Number}", x => x.Code);
    var features = new List<object>();

    foreach (var element in document.RootElement.GetProperty("elements").EnumerateArray())
    {
        if (!element.TryGetProperty("geometry", out var geometry) || geometry.GetArrayLength() < 2)
            continue;

        var coordinates = geometry.EnumerateArray()
            .Select(p => new[]
            {
                Math.Round(p.GetProperty("lat").GetDouble(), 5),
                Math.Round(p.GetProperty("lon").GetDouble(), 5),
            })
            .ToArray();

        element.TryGetProperty("tags", out var tags);
        var buildingName = Tag(tags, "name");

        if (Tag(tags, "building") != null)
        {
            var address = $"{Tag(tags, "addr:street")}|{Tag(tags, "addr:housenumber")}";

            if (buildingName != null && codesByName.TryGetValue(buildingName, out var code))
                features.Add(new { k = "z", code, c = coordinates });
            else if (codesByAddress.TryGetValue(address, out var addressCode))
                features.Add(new { k = "z", code = addressCode, c = coordinates });
            else
                features.Add(new { k = "b", c = coordinates });
        }
        else if (Tag(tags, "highway") is { } highway)
        {
            if (roadClasses.TryGetValue(highway, out var klass))
                features.Add(new { k = "r", w = klass, c = coordinates });
        }
        else if (Tag(tags, "natural") == "water")
        {
            features.Add(new { k = "w", c = coordinates });
        }
        else
        {
            // a river or stream is a line, not an area: filling it draws a blob
            features.Add(new { k = "s", c = coordinates });
        }
    }

    return new Campus(
        id,
        name,
        [[south, west], [north, east]],
        members.Select(x => (object)new { code = x.Code, lat = x.Lat, lon = x.Lon }).ToList(),
        features);
}



static string Slug(string name)
{
    var slug = new string(name.ToLowerInvariant()
        .Replace("ä", "a").Replace("ö", "o").Replace("ü", "u").Replace("ß", "ss")
        .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')
        .ToArray());

    while (slug.Contains("--"))
        slug = slug.Replace("--", "-");

    return slug.Trim('-');
}



static string? Tag(JsonElement tags, string key) =>
    tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty(key, out var value) ? value.GetString() : null;

static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

record Building(string Code, string Name, string? Street, string? Number, double Lat, double Lon);

record Campus(string id, string name, double[][] bounds, List<object> buildings, List<object> features);
