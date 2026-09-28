// Renders Surl's published coverage report from the JSON that Measure-CodeQuality.ps1
// -JsonPath writes: an HTML page (index.html) and a README badge (badge.svg), both
// self-contained, into the output directory, beside a copy of the data (coverage.json).
//
//   dotnet run --file make-coverage-report.cs -- <coverage.json> <output directory> [commit sha]
//
// Base class library only, like everything in Surl: System.Text.Json reads the data and
// the HTML and SVG are written by hand.

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: make-coverage-report.cs <coverage.json> <output directory> [commit sha]");
    return 2;
}

string dataPath = args[0];
string outDir = args[1];
string commit = args.Length > 2 ? args[2] : "";
Directory.CreateDirectory(outDir);

// PowerShell's Out-File -Encoding utf8 writes a BOM; JsonDocument wants it gone.
string json = File.ReadAllText(dataPath).TrimStart('﻿');
using JsonDocument doc = JsonDocument.Parse(json);
JsonElement root = doc.RootElement;

File.WriteAllText(Path.Combine(outDir, "coverage.json"), json, new UTF8Encoding(false));
File.WriteAllText(Path.Combine(outDir, "badge.svg"), Report.Badge(root), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(outDir, "index.html"), Report.Page(root, commit), new UTF8Encoding(false));
Console.WriteLine($"coverage report written to {outDir}");
return 0;

static class Report
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static string H(string? text) => WebUtility.HtmlEncode(text ?? "");

    static double D(JsonElement e, string name) => e.GetProperty(name).GetDouble();

    static int I(JsonElement e, string name) => e.GetProperty(name).GetInt32();

    static string S(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static string Pct(double value) => value.ToString(value >= 99.995 ? "0" : "0.0#", Inv) + "%";

    // PowerShell serialises a one-element array as the element itself; accept both.
    static IEnumerable<JsonElement> Items(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out JsonElement v)) return [];
        return v.ValueKind switch
        {
            JsonValueKind.Array => v.EnumerateArray().ToArray(),
            JsonValueKind.Object => [v],
            _ => [],
        };
    }

    static string Fails(JsonElement member) =>
        member.TryGetProperty("fails", out JsonElement f)
            ? f.ValueKind == JsonValueKind.Array ? string.Join(" ", f.EnumerateArray().Select(x => x.GetString())) : f.GetString() ?? ""
            : "";

    public static string Badge(JsonElement root)
    {
        JsonElement t = root.GetProperty("totals");
        string label = "coverage";
        string value = $"{Pct(D(t, "linePercent"))} lines · {Pct(D(t, "branchPercent"))} branches";
        string color = I(t, "failingMembers") == 0 ? "#2e7d32" : D(t, "linePercent") >= 95 ? "#b58900" : "#c62828";
        int lw = 8 + (int)Math.Ceiling(label.Length * 6.6);
        int vw = 10 + (int)Math.Ceiling(value.Length * 6.3);
        int w = lw + vw;
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="20" role="img" aria-label="{H(label)}: {H(value)}">
              <title>{H(label)}: {H(value)}</title>
              <rect width="{w}" height="20" rx="3" fill="#555"/>
              <rect x="{lw}" width="{vw}" height="20" rx="3" fill="{color}"/>
              <rect x="{lw}" width="4" height="20" fill="{color}"/>
              <g fill="#fff" font-family="Verdana,DejaVu Sans,sans-serif" font-size="11" text-anchor="middle">
                <text x="{lw / 2}" y="14">{H(label)}</text>
                <text x="{lw + vw / 2}" y="14">{H(value)}</text>
              </g>
            </svg>
            """;
    }

    static string Bar(double percent, double gate) =>
        $"<div class=\"bar\"><span class=\"{(percent >= gate ? "ok" : "low")}\" style=\"width:{Math.Clamp(percent, 0, 100).ToString("0.##", Inv)}%\"></span></div>";

    public static string Page(JsonElement root, string commit)
    {
        JsonElement t = root.GetProperty("totals");
        JsonElement g = root.GetProperty("thresholds");
        double lineGate = D(g, "linePercent"), branchGate = D(g, "branchPercent");
        var sb = new StringBuilder();
        string measured = S(root, "measuredAt");
        string commitLink = commit.Length >= 7
            ? $" from commit <a href=\"https://github.com/StewartScottRogers/Surl/commit/{H(commit)}\"><code>{H(commit[..7])}</code></a>"
            : "";

        sb.Append($$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Surl coverage</title>
            <style>
              :root { --bg:#ffffff; --fg:#1f2328; --muted:#59636e; --line:#d1d9e0; --card:#f6f8fa; --ok:#2e7d32; --low:#c62828; --track:#e5e8eb; }
              @media (prefers-color-scheme: dark) { :root { --bg:#0d1117; --fg:#e6edf3; --muted:#9198a1; --line:#30363d; --card:#161b22; --ok:#3fb950; --low:#f85149; --track:#30363d; } }
              * { box-sizing:border-box; }
              body { margin:0; background:var(--bg); color:var(--fg); font:15px/1.5 -apple-system,"Segoe UI",Roboto,sans-serif; }
              main { max-width:1100px; margin:0 auto; padding:24px 16px 48px; }
              h1 { margin:0 0 4px; font-size:26px; } h2 { margin:32px 0 8px; font-size:19px; }
              p.meta { margin:0; color:var(--muted); }
              a { color:inherit; }
              .cards { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:12px; margin-top:20px; }
              .card { background:var(--card); border:1px solid var(--line); border-radius:8px; padding:14px 16px; }
              .card .n { font-size:28px; font-weight:600; } .card .l { color:var(--muted); font-size:13px; }
              .wrap { overflow-x:auto; }
              table { width:100%; border-collapse:collapse; font-size:14px; }
              th, td { text-align:left; padding:7px 8px; border-bottom:1px solid var(--line); white-space:nowrap; }
              th { color:var(--muted); font-weight:600; } td.num, th.num { text-align:right; font-variant-numeric:tabular-nums; }
              td.wide { white-space:normal; }
              .bar { width:120px; height:8px; background:var(--track); border-radius:4px; overflow:hidden; display:inline-block; vertical-align:middle; }
              .bar span { display:block; height:100%; } .bar .ok { background:var(--ok); } .bar .low { background:var(--low); }
              .pass { color:var(--ok); font-weight:600; } .fail { color:var(--low); font-weight:600; }
              code { font:13px ui-monospace,Consolas,monospace; }
            </style>
            </head>
            <body>
            <main>
            <h1>Surl coverage</h1>
            <p class="meta">Measured {{H(measured)}}{{commitLink}} on Windows by <code>Measure-CodeQuality.ps1</code>. Every library is held to
            {{Pct(lineGate)}} line and {{Pct(branchGate)}} branch coverage, cyclomatic complexity of at most {{I(g, "complexity")}} and a CRAP score of at most {{D(g, "crap").ToString(Inv)}}.</p>
            <div class="cards">
              <div class="card"><div class="n">{{Pct(D(t, "linePercent"))}}</div><div class="l">lines covered ({{I(t, "linesCovered"):N0}} of {{I(t, "linesTotal"):N0}})</div></div>
              <div class="card"><div class="n">{{Pct(D(t, "branchPercent"))}}</div><div class="l">branches covered ({{I(t, "branchesCovered"):N0}} of {{I(t, "branchesTotal"):N0}})</div></div>
              <div class="card"><div class="n">{{I(t, "librariesAtGate")}} / {{I(t, "libraries")}}</div><div class="l">libraries meeting every gate</div></div>
              <div class="card"><div class="n">{{I(t, "failingMembers")}}</div><div class="l">members outside a gate</div></div>
            </div>
            <h2>Libraries</h2>
            <div class="wrap"><table>
            <tr><th>Library</th><th class="num">Lines</th><th></th><th class="num">Branches</th><th></th><th class="num">Members</th><th class="num">Failing</th><th class="num">Worst CRAP</th><th>Gate</th></tr>
            """);

        foreach (JsonElement lib in Items(root, "libraries").OrderBy(l => S(l, "name"), StringComparer.Ordinal))
        {
            double lp = D(lib, "linePercent"), bp = D(lib, "branchPercent");
            bool pass = I(lib, "failingMembers") == 0;
            sb.Append($"<tr><td><code>{H(S(lib, "name"))}</code></td><td class=\"num\">{Pct(lp)}</td><td>{Bar(lp, lineGate)}</td>")
              .Append($"<td class=\"num\">{Pct(bp)}</td><td>{Bar(bp, branchGate)}</td><td class=\"num\">{I(lib, "members")}</td>")
              .Append($"<td class=\"num\">{I(lib, "failingMembers")}</td><td class=\"num\">{D(lib, "worstCrap").ToString("0.##", Inv)}</td>")
              .Append($"<td class=\"{(pass ? "pass" : "fail")}\">{(pass ? "pass" : "fail")}</td></tr>\n");
        }
        sb.Append("</table></div>\n");

        JsonElement[] failing = Items(root, "failing").ToArray();
        sb.Append("<h2>Members outside a gate</h2>\n");
        if (failing.Length == 0)
        {
            sb.Append("<p>None. Every member meets every gate.</p>\n");
        }
        else
        {
            sb.Append("<div class=\"wrap\"><table>\n<tr><th>Member</th><th>Where</th><th class=\"num\">Lines</th><th class=\"num\">Branches</th><th class=\"num\">Cx</th><th class=\"num\">CRAP</th><th>Fails</th><th>Uncovered lines</th></tr>\n");
            foreach (JsonElement m in failing.OrderByDescending(m => D(m, "crap")))
            {
                string file = S(m, "file").Replace('\\', '/');
                string where = file.Length > 0
                    ? $"<a href=\"https://github.com/StewartScottRogers/Surl/blob/{H(commit.Length > 0 ? commit : "work/dark-factory")}/{H(file)}#L{I(m, "line")}\"><code>{H(file)}:{I(m, "line")}</code></a>"
                    : "";
                sb.Append($"<tr><td><code>{H(S(m, "member"))}</code></td><td>{where}</td><td class=\"num\">{Pct(D(m, "linePercent"))}</td>")
                  .Append($"<td class=\"num\">{Pct(D(m, "branchPercent"))}</td><td class=\"num\">{I(m, "complexity")}</td><td class=\"num\">{D(m, "crap").ToString("0.##", Inv)}</td>")
                  .Append($"<td>{H(Fails(m))}</td><td class=\"wide\">{H(S(m, "uncoveredLines"))}</td></tr>\n");
            }
            sb.Append("</table></div>\n");
        }

        JsonElement[] exclusions = Items(root, "exclusions").ToArray();
        sb.Append("<h2>Coverage exclusions in production code</h2>\n");
        sb.Append(exclusions.Length == 0
            ? "<p>None. Every production member is measured.</p>\n"
            : "<ul>" + string.Concat(exclusions.Select(x => $"<li><code>{H(S(x, "file").Replace('\\', '/'))}:{I(x, "line")}</code> - <code>{H(S(x, "text"))}</code></li>")) + "</ul>\n");

        sb.Append("<p class=\"meta\" style=\"margin-top:32px\">Regenerated by <code>.github/workflows/gource.yml</code> on the Gource schedule. Source: <a href=\"https://github.com/StewartScottRogers/Surl\">github.com/StewartScottRogers/Surl</a>.</p>\n</main>\n</body>\n</html>\n");
        return sb.ToString();
    }
}
