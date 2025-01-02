using Conesoft.Files;
using Conesoft.Hosting;
using Microsoft.AspNetCore.Http.Extensions;
using MonoTorrent;
using MonoTorrent.Client;

namespace Conesoft.Website.TorrentKontrol.Services
{
    public class Torrents(IConfiguration configuration, HostEnvironment environment) : IHostedService
    {
        record Engine(ClientEngine TorrentEngine, Files.Directory DownloadFolder, Files.File StateFile, string ConesoftSecret, CancellationTokenSource CancellationTokenSource);

        static Engine? engine;

        public Action? Update { get; set; }

        private void RefreshTorrentList()
        {
            try
            {
                if (engine != null)
                {
                    var torrents = GetAllTorrents();
                    foreach (var directory in engine.DownloadFolder.Directories)
                    {
                        var active = torrents.Where(t => t.State == TorrentState.Downloading || t.State == TorrentState.Starting).Any(t => t.Torrent?.Name == directory.Name);
                        if (active && directory.Info.Attributes.HasFlag(FileAttributes.Hidden) == false)
                        {
                            directory.Info.Attributes |= FileAttributes.Hidden;
                        }
                        else if (active == false && directory.Info.Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            directory.Info.Attributes &= ~FileAttributes.Hidden;
                        }
                    }
                    foreach (var file in engine.DownloadFolder.Files)
                    {
                        var active = torrents.Where(t => t.State == TorrentState.Downloading || t.State == TorrentState.Starting).Any(t => t.Torrent?.Name == file.Name);
                        if (active && file.Info.Attributes.HasFlag(FileAttributes.Hidden) == false)
                        {
                            file.Info.Attributes |= FileAttributes.Hidden;
                        }
                        else if (active == false && file.Info.Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            file.Info.Attributes &= ~FileAttributes.Hidden;
                        }
                    }
                }
            }
            finally
            {
                Update?.Invoke();
            }
        }

        public IList<TorrentManager> GetAllTorrents()
        {
            return engine?.TorrentEngine.Torrents ?? [];
        }

        public async Task Add(MagnetLink magnet)
        {
            if (engine != null)
            {
                var torrent = await engine.TorrentEngine.AddAsync(magnet, engine.DownloadFolder.Path);
                await torrent.StartAsync();
                await engine.TorrentEngine.SaveStateAsync(engine.StateFile.Path);
            }
        }

        public static async Task Add(byte[] torrentBytes)
        {
            if (engine != null)
            {
                var torrentFile = Torrent.Load(torrentBytes);
                var possibleDirectory = engine.DownloadFolder / torrentFile.Name;
                var possibleFile = engine.DownloadFolder / Filename.FromExtended(torrentFile.Name);
                if (possibleDirectory.Exists == false && possibleFile.Exists == false)
                {
                    var torrent = await engine.TorrentEngine.AddAsync(torrentFile, engine.DownloadFolder.Path);
                    await torrent.StartAsync();
                    await engine.TorrentEngine.SaveStateAsync(engine.StateFile.Path);
                }
            }
        }

        public async Task Remove(TorrentManager torrent)
        {
            try
            {
                await torrent.StopAsync();
                if (engine != null)
                {
                    await engine.TorrentEngine.RemoveAsync(torrent);
                    // TODO: Delete Files here
                }
            }
            catch (Exception)
            {
            }
            RefreshTorrentList();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var conesoftSecret = configuration["conesoft:secret"] ?? throw new Exception("Conesoft Secret not found in Configuration");
            var config = configuration.Get<Config>() ?? throw new FileNotFoundException("Configuration not Found");

            var downloadFolder = Files.Directory.From(config.DownloadUrl);

            var state = environment.Local.Storage;

            var cache = state / "Cache";
            var stateFile = state / Filename.From("Torrents", "settings");

            var clientEngine = default(ClientEngine);

            try
            {
                clientEngine = await ClientEngine.RestoreStateAsync(stateFile.Path);
            }
            catch
            {
                clientEngine = new(new EngineSettingsBuilder()
                {
                    AllowPortForwarding = true,
                    AutoSaveLoadDhtCache = true,
                    AutoSaveLoadFastResume = true,
                    AutoSaveLoadMagnetLinkMetadata = true,
                    CacheDirectory = cache.Path
                }.ToSettings());

                foreach (var file in (cache / "metadata").Filtered("*.torrent", allDirectories: false))
                {
                    var t = await clientEngine.AddAsync(await Torrent.LoadAsync(file.Path), downloadFolder.Path);
                }
            }

            await clientEngine.UpdateSettingsAsync(new EngineSettingsBuilder(clientEngine.Settings)
            {
                AllowLocalPeerDiscovery = false,
                MaximumConnections = 150,
                MaximumHalfOpenConnections = 8
            }.ToSettings());

            await clientEngine.StartAllAsync();

            clientEngine.CriticalException += Engine_CriticalException;

            clientEngine.StatsUpdate += Engine_StatsUpdate;

            var cts = new CancellationTokenSource();

            var torrentAutomation = Task.Run(async () =>
            {
                while (cts.IsCancellationRequested == false)
                {
                    foreach (var torrent in clientEngine.Torrents.ToArray())
                    {
                        switch (torrent.State)
                        {
                            case TorrentState.Seeding:
                                await torrent.StopAsync();
                                await clientEngine.RemoveAsync(torrent);
                                await Notify(
                                    title: $"{torrent.Torrent?.Name ?? ""} Finished",
                                    message: $"The Torrent '{torrent.Torrent?.Name ?? ""}' successfully finished downloading",
                                    url: $"https://files.conesoft.net/Downloads/Torrents/{torrent.Torrent?.Name ?? ""}"
                                );
                                break;
                        }
                    }
                    await clientEngine.SaveStateAsync(stateFile.Path);
                    await Task.Delay(1000);
                    RefreshTorrentList();
                }
            }, cancellationToken);
            engine = new Engine(clientEngine, downloadFolder, stateFile, conesoftSecret, cts);
        }

        private void Engine_StatsUpdate(object? sender, StatsUpdateEventArgs e)
        {
            RefreshTorrentList();
        }

        private async void Engine_CriticalException(object? sender, CriticalExceptionEventArgs e)
        {
            Console.WriteLine("Critical Exception in Torrents");
            Console.WriteLine(e.Exception.Message);

            await StopAsync(CancellationToken.None);
            await StartAsync(CancellationToken.None);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (engine != null)
            {
                await engine.CancellationTokenSource.CancelAsync();
                await engine.TorrentEngine.StopAllAsync();
            }
        }

        record Link(string Url, string Name);
        record Config(string DownloadUrl, Link[] Links);

        static async Task Notify(string title, string message, string url)
        {
            try
            {
                if (engine != null)
                {
                    var query = new QueryBuilder
                {
                    { "token", engine.ConesoftSecret },
                    { "title", title },
                    { "message", message },
                    { "url", url }
                };

                    await new HttpClient().GetAsync($@"https://conesoft.net/notify" + query.ToQueryString());
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
