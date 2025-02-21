using Conesoft.Files;
using Conesoft.Hosting;
using Conesoft.Tools;
using Conesoft.Website.TorrentKontrol.Configuration;
using Microsoft.Extensions.Options;
using MonoTorrent;
using MonoTorrent.Client;
using Serilog;

namespace Conesoft.Website.TorrentKontrol.Features.Runtime;

public class TorrentEngine(ClientEngine engine, Files.Directory downloadFolder, Files.File stateFile) : IAsyncDisposable
{
    public IReadOnlyCollection<TorrentManager> Torrents => engine.Torrents.AsReadOnly();

    public async Task Add(MagnetLink magnet)
    {
        Log.Information("add new magnet link: {magnet}", magnet.Name);
        var torrent = await engine.AddAsync(magnet, downloadFolder.Path);
        await torrent.StartAsync();
        await engine.SaveStateAsync(stateFile.Path);
    }

    public async Task Add(byte[] torrentBytes)
    {
        Log.Information("trying to add new torrent");
        var torrentFile = Torrent.Load(torrentBytes);

        Log.Information("add new torrent: {torrent}", torrentFile.Name);

        var possibleDirectory = downloadFolder / torrentFile.Name;
        var possibleFile = downloadFolder / Filename.FromExtended(torrentFile.Name);
        if (possibleDirectory.Exists == false && possibleFile.Exists == false)
        {
            var torrent = await engine.AddAsync(torrentFile, downloadFolder.Path);
            await torrent.StartAsync();
            await engine.SaveStateAsync(stateFile.Path);
        }
    }

    public async Task Remove(TorrentManager torrent)
    {
        Log.Information("remove torrent: {torrent}", torrent.Name);
        await torrent.StopAsync();
        await engine.RemoveAsync(torrent, RemoveMode.CacheDataAndDownloadedData);
    }

    public async Task<IEnumerable<TorrentManager>> CleanupSeeds()
    {
        var seeding = engine.Torrents.NotNull().Where(t => t.State == TorrentState.Seeding).ToArray();
        foreach (var torrent in seeding)
        {
            await Safe.TryAsync(torrent.StopAsync);
            await Safe.TryAsync(async () => await engine.RemoveAsync(torrent));
        }
        await Safe.TryAsync(async () => await engine.SaveStateAsync(stateFile.Path));
        if(seeding.Length > 0)
        {
            Log.Information("cleaned up {amount} torrents", seeding.Length);
        }
        return seeding;
    }

    public event EventHandler<CriticalExceptionEventArgs>? CriticalException
    {
        add => engine.CriticalException += value;
        remove => engine.CriticalException -= value;
    }

    public event EventHandler<StatsUpdateEventArgs>? StatsUpdate
    {
        add => engine.StatsUpdate += value;
        remove => engine.StatsUpdate -= value;
    }

    private static async Task<ClientEngine?> RestoreClientEngine(string stateFilePath)
    {
        Log.Information("trying to restore client engine");
        return await Safe.TryAsync(async () => await ClientEngine.RestoreStateAsync(stateFilePath));
    }

    private static async Task<ClientEngine> CreateClientEngine(HostEnvironment environment, string downloadPath)
    {
        Log.Information("creating new client engine");
        var cache = environment.Local.Storage / "Cache";

        var engine = new ClientEngine(new EngineSettingsBuilder()
        {
            AllowPortForwarding = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = cache.Path            
        }.ToSettings());

        foreach (var file in (cache / "metadata").FilteredFiles("*.torrent", allDirectories: false))
        {
            await engine.AddAsync(await Torrent.LoadAsync(file.Path), downloadPath);
        }

        return engine;
    }

    public static async Task<TorrentEngine> StartEngine(IOptions<Config> config, HostEnvironment environment, Action<ClientEngine> bindEvents)
    {
        Log.Information("setting up torrent engine");
        var downloadFolder = Files.Directory.From(config.Value.DownloadUrl);
        var stateFile = environment.Local.Storage / Filename.From("Torrents", "settings");

        var engine = await RestoreClientEngine(stateFile.Path) ?? await CreateClientEngine(environment, downloadFolder.Path);

        await engine.UpdateSettingsAsync(new EngineSettingsBuilder(engine.Settings)
        {
            AllowLocalPeerDiscovery = false,
            MaximumConnections = 150,
            MaximumHalfOpenConnections = 8
        }.ToSettings());

        bindEvents(engine);

        Log.Information("starting torrent engine");
        await engine.StartAllAsync();

        return new(engine, downloadFolder, stateFile);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        Log.Information("stopping torrent engine");
        await engine.StopAllAsync();
        engine.Dispose();
        GC.SuppressFinalize(this);
    }
}