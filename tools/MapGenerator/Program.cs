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
    ["ZT"] = "Campus Zentrum, Toni-Areal",
    ["ZA"] = "Campus Zentrum, Lagerstrasse",
    ["ZL"] = "Campus Zentrum, Lagerstrasse",
};

// The towns ZHAW sits in. An address lookup is confined to one of them, so a street
// name that repeats elsewhere in Switzerland cannot match the wrong building.
var winterthur = (South: 47.47, West: 8.68, North: 47.53, East: 8.78);
var zurich = (South: 47.36, West: 8.50, North: 47.40, East: 8.56);

// Hand-verified addresses, for the buildings OSM does not name after their code. The
// address may sit on the outline itself or on a bare address node inside it; either way
// it resolves to the enclosing outline.
var buildingsByAddress = new Dictionary<string, (string Street, string Number, (double South, double West, double North, double East) Area)>
{
    ["ME"] = ("Tössfeldstrasse", "27", winterthur),
    ["MG"] = ("Katharina-Sulzer-Platz", "9", winterthur),
    ["MU"] = ("Zürcherstrasse", "12", winterthur),
    ["MW"] = ("Technoparkstrasse", "1", winterthur),
    ["SG"] = ("Gertrudstrasse", "15", winterthur),
    ["TU"] = ("Technikumstrasse", "81", winterthur),
    ["ZA"] = ("Militärstrasse", "48", zurich),
    ["ZL"] = ("Lagerstrasse", "41", zurich),
    ["ZT"] = ("Pfingstweidstrasse", "96", zurich),
};

// Hand-verified OSM elements, for the buildings with no usable address either. A way is
// the outline itself; a node or a bare "lat/lon" is a point inside it - several ZHAW
// buildings are mapped only as a defibrillator or toilet POI - and the outline that
// contains that point is looked up.
//
// A code may list several: ZL occupies two neighbouring buildings, and gets its second
// here on top of the address above.
var buildingsByOsm = new Dictionary<string, string[]>
{
    ["GE"] = ["node/11331206153"],
    ["GQ"] = ["way/146301271"],
    ["GS"] = ["way/146301338"],
    ["GU"] = ["node/13033898930"],
    ["RA"] = ["47.225091/8.679146"],
    ["RD"] = ["node/11037266739"],
    ["RN"] = ["way/201594465"],
    ["RS"] = ["node/10763310022"],
    ["RT"] = ["node/10763310021"],
    ["ZL"] = ["way/409968050"],
};

// Codes ZHAW lists separately that share one outline in OSM. Both codes then label and
// highlight the same shape, which is honest about what OSM knows.
//
// TB is deliberately absent: it may share TH's building, but it may equally no longer
// exist, and that has to be seen on site before it is drawn anywhere.
var buildingsSharedWith = new Dictionary<string, string>
{
    ["RR"] = "RS",
    ["TM"] = "TE",
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

        // the id is kept so a code sharing this building can borrow the same outline
        var ways = element.GetProperty("type").GetString() == "way"
            ? new List<long> { element.GetProperty("id").GetInt64() }
            : [];

        found[code] = new Building(code, name, ways,
            centre.GetProperty("lat").GetDouble(),
            centre.GetProperty("lon").GetDouble());
    }

    foreach (var (code, building) in await ResolvePinned())
        found[code] = building;

    // codes that share another code's outline, resolved last so the target already exists
    foreach (var (code, shared) in buildingsSharedWith)
    {
        if (!found.TryGetValue(shared, out var target))
        {
            Console.WriteLine($"  {code}: shares {shared}, which was not found");
            continue;
        }

        if (target.WayIds.Count == 0)
            Console.WriteLine($"  {code}: shares {shared}, which has no outline to share");

        found[code] = target with { Code = code, Name = $"ZHAW {code}" };
    }

    return found.Values.ToList();
}

// Resolves every hand-verified code to the OSM way of its outline, in three batched
// queries rather than one per building. Matching on the way id afterwards is exact,
// where matching on a name or an address is not.
async Task<Dictionary<string, Building>> ResolvePinned()
{
    var outlines = new Dictionary<string, List<(long Id, double Lat, double Lon)>>();  // code -> its outlines
    var points = new List<(string Code, double Lat, double Lon)>();                    // still needing an outline

    void Add(string code, long id, double lat, double lon)
    {
        if (!outlines.TryGetValue(code, out var list))
            outlines[code] = list = new List<(long Id, double Lat, double Lon)>();

        if (list.All(x => x.Id != id))
            list.Add((id, lat, lon));
    }

    var wayIds = new Dictionary<long, string>();
    var nodeIds = new Dictionary<long, string>();

    foreach (var (code, references) in buildingsByOsm)
    foreach (var reference in references)
    {
        if (reference.StartsWith("way/"))
            wayIds[long.Parse(reference[4..])] = code;
        else if (reference.StartsWith("node/"))
            nodeIds[long.Parse(reference[5..])] = code;
        else
        {
            var parts = reference.Split('/');
            points.Add((code, double.Parse(parts[0], CultureInfo.InvariantCulture),
                              double.Parse(parts[1], CultureInfo.InvariantCulture)));
        }
    }

    // 1. the elements named outright
    if (wayIds.Count > 0 || nodeIds.Count > 0)
    {
        var clauses = "";
        if (wayIds.Count > 0) clauses += $"way(id:{string.Join(",", wayIds.Keys)});";
        if (nodeIds.Count > 0) clauses += $"node(id:{string.Join(",", nodeIds.Keys)});";

        using var document = await Query($"[out:json][timeout:90];({clauses});out center;");

        foreach (var element in document.RootElement.GetProperty("elements").EnumerateArray())
        {
            var id = element.GetProperty("id").GetInt64();

            if (element.GetProperty("type").GetString() == "way" && wayIds.TryGetValue(id, out var wayCode))
            {
                var centre = element.GetProperty("center");
                Add(wayCode, id, centre.GetProperty("lat").GetDouble(), centre.GetProperty("lon").GetDouble());
            }
            else if (nodeIds.TryGetValue(id, out var nodeCode))
            {
                points.Add((nodeCode, element.GetProperty("lat").GetDouble(), element.GetProperty("lon").GetDouble()));
            }
        }
    }

    // 2. the addresses
    if (buildingsByAddress.Count > 0)
    {
        var clauses = string.Concat(buildingsByAddress.Values.Select(address =>
        {
            var box = $"{F(address.Area.South)},{F(address.Area.West)},{F(address.Area.North)},{F(address.Area.East)}";
            // anchored, so "1" cannot also match 10 and 12; a letter suffix still counts
            var number = $"[\"addr:housenumber\"~\"^{address.Number}[a-z]?$\"]";
            return $"way[\"building\"][\"addr:street\"=\"{address.Street}\"]{number}({box});"
                 + $"node[\"addr:street\"=\"{address.Street}\"]{number}({box});";
        }));

        using var document = await Query($"[out:json][timeout:120];({clauses});out tags center;");

        var codesByAddress = buildingsByAddress.ToDictionary(x => $"{x.Value.Street}|{x.Value.Number}", x => x.Key);
        var fromOutline = new HashSet<string>();

        // an outline carrying the address wins over a bare address node inside one
        foreach (var element in document.RootElement.GetProperty("elements").EnumerateArray()
                     .OrderBy(x => x.GetProperty("type").GetString() == "way" ? 0 : 1))
        {
            element.TryGetProperty("tags", out var tags);
            var digits = new string((Tag(tags, "addr:housenumber") ?? "").TakeWhile(char.IsAsciiDigit).ToArray());

            if (!codesByAddress.TryGetValue($"{Tag(tags, "addr:street")}|{digits}", out var code))
                continue;

            if (element.GetProperty("type").GetString() == "way")
            {
                var centre = element.GetProperty("center");
                Add(code, element.GetProperty("id").GetInt64(),
                    centre.GetProperty("lat").GetDouble(), centre.GetProperty("lon").GetDouble());
                fromOutline.Add(code);
            }
            else if (!fromOutline.Contains(code))
            {
                points.Add((code, element.GetProperty("lat").GetDouble(), element.GetProperty("lon").GetDouble()));
            }
        }
    }

    // 3. the outlines around whatever is still only a point
    if (points.Count > 0)
    {
        var clauses = string.Concat(points.Select(p => $"way(around:40,{F(p.Lat)},{F(p.Lon)})[\"building\"];"));
        using var document = await Query($"[out:json][timeout:120];({clauses});out geom;");

        var candidates = document.RootElement.GetProperty("elements").EnumerateArray()
            .Where(x => x.TryGetProperty("geometry", out var g) && g.GetArrayLength() > 2)
            .Select(x => (
                Id: x.GetProperty("id").GetInt64(),
                Ring: x.GetProperty("geometry").EnumerateArray()
                    .Select(p => (Lat: p.GetProperty("lat").GetDouble(), Lon: p.GetProperty("lon").GetDouble()))
                    .ToArray()))
            .ToList();

        foreach (var (code, lat, lon) in points)
        {
            var match = candidates.FirstOrDefault(x => Contains(x.Ring, lat, lon));

            if (match.Ring == null)
            {
                Console.WriteLine($"  {code}: no building outline contains {F(lat)},{F(lon)}");
                continue;
            }

            Add(code, match.Id,
                (match.Ring.Min(p => p.Lat) + match.Ring.Max(p => p.Lat)) / 2,
                (match.Ring.Min(p => p.Lon) + match.Ring.Max(p => p.Lon)) / 2);
        }
    }

    foreach (var code in buildingsByAddress.Keys.Concat(buildingsByOsm.Keys).Where(x => !outlines.ContainsKey(x)))
        Console.WriteLine($"  {code}: could not be located");

    // a code spanning several buildings sits between them
    return outlines.ToDictionary(x => x.Key, x => new Building(
        x.Key, $"ZHAW {x.Key}", x.Value.Select(y => y.Id).ToList(),
        x.Value.Average(y => y.Lat), x.Value.Average(y => y.Lon)));
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
        $"way[\"railway\"~\"^(rail|light_rail|narrow_gauge|tram|subway)$\"]({box});" +
        $"way[\"natural\"=\"water\"]({box});" +
        ");out geom;");

    // a code is matched on its own OSM way where one is known, and on the OSM name
    // otherwise. Both are many-to-many: codes can share one outline, and one code
    // can span several.
    var codesByWay = members
        .SelectMany(x => x.WayIds.Select(id => (Id: id, x.Code)))
        .GroupBy(x => x.Id)
        .ToDictionary(x => x.Key, x => x.Select(y => y.Code).ToList());

    var codesByName = members.ToLookup(x => x.Name, x => x.Code);

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
            var codes = new List<string>();

            if (codesByWay.TryGetValue(element.GetProperty("id").GetInt64(), out var byWay))
                codes.AddRange(byWay);

            if (buildingName != null)
                codes.AddRange(codesByName[buildingName].Where(x => !codes.Contains(x)));

            if (codes.Count > 0)
                features.Add(new { k = "z", codes = codes.Order(StringComparer.Ordinal).ToList(), c = coordinates });
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
        else if (Tag(tags, "railway") != null)
        {
            features.Add(new { k = "t", c = coordinates });
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



// ray casting; the OSM ring is closed, so the wrap-around edge is covered
static bool Contains((double Lat, double Lon)[] ring, double lat, double lon)
{
    var inside = false;

    for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
    {
        if (ring[i].Lon > lon != ring[j].Lon > lon
            && lat < (ring[j].Lat - ring[i].Lat) * (lon - ring[i].Lon) / (ring[j].Lon - ring[i].Lon) + ring[i].Lat)
            inside = !inside;
    }

    return inside;
}

static string? Tag(JsonElement tags, string key) =>
    tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty(key, out var value) ? value.GetString() : null;

static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

record Building(string Code, string Name, List<long> WayIds, double Lat, double Lon);

record Campus(string id, string name, double[][] bounds, List<object> buildings, List<object> features);
