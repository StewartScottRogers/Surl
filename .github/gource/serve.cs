// Serves a render directory over HTTP for a local preview of the viewer page.
//
// Usage: dotnet run --file .github/gource/serve.cs -- <render directory> [port]
//        then open http://localhost:8000/ (Ctrl+C stops it)
//
// Copy .github/gource/site/index.html into the render directory first. The page loads its
// HLS playlists with fetch, which browsers refuse from file:// - hence a server. Local
// preview only: it answers on localhost and serves files under the directory given.
using System.Net;

string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
int port = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 8000;
var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    [".html"] = "text/html; charset=utf-8",
    [".json"] = "application/json",
    [".txt"] = "text/plain; charset=utf-8",
    [".m3u8"] = "application/vnd.apple.mpegurl",
    [".m4s"] = "video/iso.segment",
    [".mp4"] = "video/mp4",
    [".gif"] = "image/gif",
    [".jpg"] = "image/jpeg",
};

using var listener = new HttpListener();
listener.Prefixes.Add($"http://localhost:{port}/");
listener.Start();
Console.WriteLine($"serving {root} at http://localhost:{port}/ (Ctrl+C to stop)");
while (true)
{
    HttpListenerContext context = await listener.GetContextAsync();
    _ = Task.Run(() => Serve(context, root, types));
}

static async Task Serve(HttpListenerContext context, string root, Dictionary<string, string> types)
{
    using HttpListenerResponse response = context.Response;
    string relative = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath).TrimStart('/');
    string path = Path.GetFullPath(Path.Combine(root, relative.Length == 0 ? "index.html" : relative));
    if (Directory.Exists(path))
    {
        path = Path.Combine(path, "index.html");
    }
    bool inside = path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    if (!inside || !File.Exists(path))
    {
        response.StatusCode = 404;
        Console.WriteLine($"404 {context.Request.Url.AbsolutePath}");
        return;
    }
    response.ContentType = types.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
    await using FileStream file = File.OpenRead(path);
    response.ContentLength64 = file.Length;
    try
    {
        await file.CopyToAsync(response.OutputStream);
    }
    catch (HttpListenerException)
    {
        // The browser cancelled the request (hls.js does when switching quality).
    }
}
