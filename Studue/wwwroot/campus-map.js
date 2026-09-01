// Campus map. Leaflet is used only as a pan/zoom/geolocate engine - there is no tile
// layer, the geometry is our own committed GeoJSON, and every colour comes from CSS.
// Leaflet itself is fetched lazily so the other pages never pay for it.

// fingerprinted urls, emitted by the page - they are the ones served as immutable, and
// the hash is only known at build time
let assetUrls = { leafletJs: "/lib/leaflet/leaflet.js", leafletCss: "/lib/leaflet/leaflet.css", campus: {} };

function readAssetUrls() {
    const tag = document.getElementById("map-urls");
    if (tag) assetUrls = JSON.parse(tag.textContent);
}

function campusUrl(id) {
    return assetUrls.campus[id] ?? `/maps/${id}.json`;
}

let leafletLoading = null;
let map = null;
let layers = null;
let currentCampus = null;

function loadLeaflet() {
    if (window.L) return Promise.resolve();
    if (leafletLoading) return leafletLoading;

    // the stylesheet is declared by the page itself; only the library is fetched here
    leafletLoading = new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = assetUrls.leafletJs;
        script.onload = resolve;
        script.onerror = () => reject(new Error("could not load leaflet"));
        document.head.appendChild(script);
    });

    return leafletLoading;
}

let pendingCampus = null;

function fetchCampus(id) {
    if (pendingCampus?.id !== id) {
        pendingCampus = { id, promise: fetch(campusUrl(id)).then(r => (r.ok ? r.json() : null)) };
    }

    return pendingCampus.promise;
}

// the panel floats over the map, so the target has to be framed in what is left of it:
// beside the panel on a wide screen, below it on a narrow one
function panelOffset() {
    const panel = document.querySelector(".map-picker");
    const host = document.getElementById("campus-map");
    if (!panel || !host) return [30, 30];

    const box = panel.getBoundingClientRect();
    const surface = host.getBoundingClientRect();

    return box.width > surface.width * 0.6
        ? [30, box.height + 30]
        : [box.width + 30, 30];
}

// buildings first by area so small ones stay clickable, ZHAW ones last so they sit on top
function draw(campus, highlight) {
    layers.clearLayers();

    const order = { w: 0, s: 1, r: 2, t: 3, b: 4, z: 5 };
    const features = [...campus.features].sort((a, b) => order[a.k] - order[b.k]);

    // a code can span several buildings, so every match is framed, not just the first
    const targets = [];

    for (const feature of features) {
        let shape;

        if (feature.k === "r") {
            shape = L.polyline(feature.c, { className: `map-road map-road-${feature.w}` });
        } else if (feature.k === "s") {
            shape = L.polyline(feature.c, { className: "map-stream" });
        } else if (feature.k === "t") {
            shape = L.polyline(feature.c, { className: "map-rail" });
        } else if (feature.k === "z") {
            const active = feature.codes.includes(highlight);
            shape = L.polygon(feature.c, { className: `map-zhaw${active ? " is-active" : ""}` });
            // several codes share one outline where OSM draws only one
            shape.bindTooltip(feature.codes.join(" / "), { permanent: true, direction: "center", className: "map-label" });
            if (active) targets.push(shape);
        } else {
            shape = L.polygon(feature.c, { className: feature.k === "w" ? "map-water" : "map-building" });
        }

        shape.addTo(layers);
    }

    if (targets.length > 0) {
        const extent = targets.reduce((bounds, shape) => bounds.extend(shape.getBounds()),
            L.latLngBounds(targets[0].getBounds()));

        // a fixed padding with a zoom cap keeps every building at a comparable scale,
        // where a proportional pad would frame a small building far too wide
        map.fitBounds(extent, {
            maxZoom: 19,
            paddingTopLeft: panelOffset(),
            paddingBottomRight: [30, 30],
        });
    } else {
        // the ZHAW buildings, not campus.bounds: that is the padded query box and
        // frames the whole town rather than the campus. Modest even padding here, not
        // panelOffset: reserving the panel's full width zooms the campus right out,
        // and a campus fills the view anyway so a corner overlap costs nothing.
        const extent = L.latLngBounds(campus.buildings.map(b => [b.lat, b.lon]));
        map.fitBounds(extent, { maxZoom: 18, padding: [50, 50] });
    }
}

// a whole campus, with nothing singled out
function setMessage(text) {
    const banner = document.getElementById("map-message");
    if (!banner) return;

    banner.textContent = text;
    banner.style.display = text ? "block" : "none";
}

// the list is rendered by the server, so filtering only hides what does not match
window.filterBuildings = function (term) {
    const needle = term.trim().toUpperCase();

    for (const item of document.querySelectorAll("#map-results li")) {
        const label = item.textContent.trim().toUpperCase();
        item.hidden = needle.length > 0 && !label.includes(needle);
    }
};

async function initCampusMap() {
    const host = document.getElementById("campus-map");
    if (!host) return;

    // enhanced navigation reuses the DOM, so a second visit would hit a stale container
    if (map) {
        map.remove();
        map = null;
    }

    readAssetUrls();

    const page = document.querySelector(".map-page");
    const campus = page?.dataset.campus;

    if (!campus) {
        setMessage("No map has been generated yet.");
        return;
    }

    // the campus file does not depend on leaflet, so it is asked for first and awaited
    // later - the two travel together instead of one after the other
    const pending = fetchCampus(campus);

    try {
        await loadLeaflet();
        currentCampus = await pending;
    } catch (error) {
        console.error(error);
        setMessage("The map could not be loaded. Please try again later.");
        return;
    }

    if (!currentCampus) {
        setMessage("No map has been generated for that campus yet.");
        return;
    }

    // no zoom buttons: scroll, pinch and double-click all zoom already.
    // zoomSnap allows fractional levels: snapping to whole ones rounds a fit down and
    // leaves a campus at roughly half the scale it could be shown at.
    map = L.map(host, {
        attributionControl: false,
        zoomControl: false,
        zoomSnap: 0.25,
        renderer: L.svg({ padding: 100 }),
    });
    layers = L.layerGroup().addTo(map);

    draw(currentCampus, page?.dataset.building?.toUpperCase() ?? null);

    // opening the dropdown should put the cursor straight in the search box
    const picker = document.getElementById("map-picker");
    const search = document.getElementById("building-search");

    picker?.addEventListener("toggle", () => {
        if (!picker.open) return;
        search.value = "";
        window.filterBuildings("");
        search.focus();
    });
}

if (window.Blazor) {
    Blazor.addEventListener("enhancedload", initCampusMap);
}

initCampusMap();
