using MonoTorrent;
using MonoTorrent.Client;
using Conesoft.Files;
using Conesoft.Hosting;
using Directory = Conesoft.Files.Directory;

var config = await Conesoft.Hosting.Host.LocalSettings.ReadFromJson<Config>();
var downloadFolder = Directory.From(config.DownloadUrl);

var state = Conesoft.Hosting.Host.LocalStorage;

var cache = state / "Cache";
var stateFile = state / Filename.From("Torrents", "settings");

using var engine = stateFile.Exists ? await ClientEngine.RestoreStateAsync(stateFile.Path) : new ClientEngine(new EngineSettingsBuilder()
{
    AllowPortForwarding = true,
    AutoSaveLoadDhtCache = true,
    AutoSaveLoadFastResume = true,
    AutoSaveLoadMagnetLinkMetadata = true,
    ListenPort = 55123,
    DhtPort = 55123,
    CacheDirectory = cache.Path
}.ToSettings());

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseHostingDefaults(useDefaultFiles: true, useStaticFiles: true);

app.MapGet("/torrents", () => engine.Torrents.Select(t => new { Name = t.Torrent?.Name ?? "<no name yet>", t.Progress, t.Size, t.Monitor.DownloadSpeed, State = Enum.GetName(t.State) }));
app.MapGet("/files", () => downloadFolder.Files.Select(f => new { Name = f.NameWithoutExtension, f.Extension }));
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
    var torrent = await engine.AddAsync(MagnetLink.Parse(uri), downloadFolder.Path);
    await torrent.StartAsync();
    await engine.SaveStateAsync(stateFile.Path);
    return "<style>html { background: black; color: green; font-family: monospace; margin: 5rem }</style>added " + uri + "<script>setTimeout(function() { window.history.back() },333)</script>";
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

record MagnetData(string Magnet);
record Config(string DownloadUrl);
