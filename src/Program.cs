using Conesoft.Files;
using Conesoft.Hosting;
using MonoTorrent;
using MonoTorrent.Client;
using System.Text.RegularExpressions;

var configuration = new ConfigurationBuilder().AddJsonFile(Conesoft.Hosting.Host.GlobalSettings.Path).Build();
var wirepusher = configuration["wirepusher:url"];

var config = await Conesoft.Hosting.Host.LocalSettings.ReadFromJson<Config>();
var downloadFolder = Conesoft.Files.Directory.From(config.DownloadUrl);

var state = Conesoft.Hosting.Host.LocalStorage;

var cache = state / "Cache";
var stateFile = state / Filename.From("Torrents", "settings");

ClientEngine engine;
try
{
    engine = await ClientEngine.RestoreStateAsync(stateFile.Path);
}
catch
{
    engine = new(new EngineSettingsBuilder()
    {
        AllowPortForwarding = true,
        AutoSaveLoadDhtCache = true,
        AutoSaveLoadFastResume = true,
        AutoSaveLoadMagnetLinkMetadata = true,
        ListenPort = 55123,
        DhtPort = 55123,
        CacheDirectory = cache.Path
    }.ToSettings());



    foreach (var file in (cache / "metadata").Filtered("*.torrent", allDirectories: false))
    {
        var t = await engine.AddAsync(await Torrent.LoadAsync(file.Path), downloadFolder.Path);
    }
}

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseHostingDefaults(useDefaultFiles: true, useStaticFiles: true);

app.MapGet("/torrents", () => engine.Torrents.Select(t => new { Hash = t.InfoHash.ToHex(), Name = t.Torrent?.Name ?? t.MagnetLink.Name ?? t.MagnetLink.InfoHash.ToHex(), t.Progress, t.Size, t.Monitor.DownloadSpeed, State = Enum.GetName(t.State) }));
app.MapGet("/cancel", (string hash) =>
{
    var _ = Task.WhenAll(engine.Torrents.Where(t => t.InfoHash.ToHex() == hash).Select(async t =>
    {
        try
        {
            await t.StopAsync();
            await engine.RemoveAsync(t);
        }
        catch { }
    }));
    return Results.Redirect("/");
});
app.MapPost("/removetorrent", (TorrentData data) =>
{
    var _ = Task.WhenAll(engine.Torrents.Where(t => t.InfoHash.ToHex() == data.Hash).Select(async t =>
    {
        try
        {
            await t.StopAsync();
            await engine.RemoveAsync(t);
        }
        catch { }
    }));
    return "ok";
});
app.MapGet("/files", () =>
{
    var files = downloadFolder.Files.Select(f => new DownloadedData(f.NameWithoutExtension, "", f.Info.Length));
    var directories = downloadFolder.Directories.Select(d => new DownloadedData(d.Name, "", d.AllFiles.Sum(f => f.Info.Length)));

    var cleanup = new[] { "2160p", "1080p", "BluRay", "AC3", "WEB", "H264", "ATMOS", "4K", "HD", "WEBrip", "ITA", "ENG", "AMZN", "WEB-DL", "DDP5", "HDR", "x264", "x265", "264", "265", "BDRip" };
    return files
    .Concat(directories)
    .Select(f => f with { Name = f.Name.Replace(".", " ") })
    .Select(f => f with { Name = Regex.Replace(f.Name, @" ?\(.*?\)", string.Empty) })
    .Select(f => f with { Name = Regex.Replace(f.Name, @" ?\{.*?\}", string.Empty) })
    .Select(f => f with { Name = Regex.Replace(f.Name, @" ?\[.*?\]", string.Empty) })
    .Select(f =>
    {
        var filtered = f.Name;
        foreach (var c in cleanup)
        {
            filtered = filtered.Replace(" " + c + " ", " ", StringComparison.OrdinalIgnoreCase);
        }
        filtered = string.Join(" ", filtered.Split().Where(s => s.Contains('-') == false));
        return f with { Name = filtered };
    })
    .OrderBy(f => f.Name);
});
app.MapPost("/addtorrent", async (MagnetData data) =>
{
    var torrent = await engine.AddAsync(MagnetLink.Parse(data.Magnet), downloadFolder.Path);
    await torrent.StartAsync();
    await engine.SaveStateAsync(stateFile.Path);
    return "ok";
});
app.MapGet("/addmagneturi", async (string uri) =>
{
    uri = Uri.UnescapeDataString(uri);
    var magnet = MagnetLink.Parse(uri);
    var torrent = await engine.AddAsync(magnet, downloadFolder.Path);
    await torrent.StartAsync();
    await engine.SaveStateAsync(stateFile.Path);
    return "" +
    "<!DOCTYPE html>" +
    "<html lang=\"en\">" +
        "<head prefix=\"og: http://ogp.me/ns#\">" +
            "<meta charset=\"utf-8\">" +
            "<style>html { background: black; }</style>" +
            "<link rel='stylesheet type='text/css' href='/style.css'>" +
            "<script>setTimeout(function() { window.history.back() }, 333)</script>" +
        "</head>" +
        "<body>" +
            "<header>" +
                "<h1>TorrentKontrol</h1>" +
            "</header>" +
            "<section>" +
                "<h1>adding torrent</h1>" +
                "<ul>" +
                    "<li>" + magnet.Name ?? magnet.ToV1Uri() + "</li>" +
                "</ul>" +
            "</section>" +
        "</body>" +
    "</html>";
});
bool firsttimelinksrequest = true;
app.MapGet("/links", async () =>
{
    var config = await Conesoft.Hosting.Host.LocalSettings.ReadFromJson<Config>();
    var links = config.Links;

    if(firsttimelinksrequest == false)
    {
        using (var http = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false }))
        {
            var changes = false;
            links = await Task.WhenAll(links.Select(async link =>
            {
                var response = await http.GetAsync("https://" + link.Url);
                if (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
                {
                    changes = true;
                    return link with { Url = response.Headers.Location.Host };
                }
                return link;
            }));
            if (changes)
            {
                await Conesoft.Hosting.Host.LocalSettings.WriteAsJson(config with { Links = links }, pretty: true);
            }
        }
    }
    firsttimelinksrequest = false;

    return links;
});
app.MapGet("/test", () => "Hi");

var server = app.RunAsync();

var torrentAutomation = Task.Run(async () =>
{
    while (app.Lifetime.ApplicationStopping.IsCancellationRequested == false)
    {
        foreach (var torrent in engine.Torrents.ToArray())
        {
            switch (torrent.State)
            {
                case TorrentState.Seeding:
                    await torrent.StopAsync();
                    await engine.RemoveAsync(torrent);
                    await Notify($"{torrent.Torrent.Name} Finished", $"The Torrent '{torrent.Torrent.Name}' successfully finished downloading", "Server");
                    break;
            }
            await engine.SaveStateAsync(stateFile.Path);
        }
        await Task.Delay(1000);
    }
});

await Task.Delay(5000);

await engine.StartAllAsync();

await Task.WhenAny(server, torrentAutomation);

Task Notify(string title, string message, string type) => new HttpClient().GetAsync(wirepusher + $@"title={title}&message={message}&type={type}");

record MagnetData(string Magnet);
record TorrentData(string Hash);

record DownloadedData(string Name, string Extension, long Size);

record Link(string Url, string Name);
record Config(string DownloadUrl, Link[] Links);
