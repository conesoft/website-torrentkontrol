using Conesoft.Files;
using Microsoft.AspNetCore.Http.Extensions;
using MonoTorrent;
using MonoTorrent.Client;

namespace Conesoft.Website.TorrentKontrol.Services
{
    public class Torrents : IHostedService
    {
        ClientEngine engine;
        Files.Directory downloadFolder;
        Files.File stateFile;
        string conesoftSecret;
        CancellationTokenSource cts;

        public Action? Update { get; set; }

        public IList<TorrentManager> GetAllTorrents()
        {
            return engine.Torrents;
        }

        public async Task Add(MagnetLink magnet)
        {
            var torrent = await engine.AddAsync(magnet, downloadFolder.Path);
            await torrent.StartAsync();
            await engine.SaveStateAsync(stateFile.Path);
        }

        public async Task Remove(TorrentManager torrent)
        {
            try
            {
                await torrent.StopAsync();
                await engine.RemoveAsync(torrent);
            }
            catch (Exception)
            {
            }
            Update?.Invoke();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var configuration = new ConfigurationBuilder().AddJsonFile(Conesoft.Hosting.Host.GlobalSettings.Path).Build();
            conesoftSecret = configuration["conesoft:secret"] ?? throw new Exception("Conesoft Secret not found in Configuration");

            var config = await Hosting.Host.LocalSettings.ReadFromJson<Config>();

            downloadFolder = Files.Directory.From(config.DownloadUrl);

            var state = Hosting.Host.LocalStorage;

            var cache = state / "Cache";
            stateFile = state / Filename.From("Torrents", "settings");

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

            await engine.StartAllAsync();

            engine.CriticalException += Engine_CriticalException;

            engine.StatsUpdate += Engine_StatsUpdate;

            var torrentAutomation = Task.Run(async () =>
            {
                cts = new CancellationTokenSource();
                while (cts.IsCancellationRequested == false)
                {
                    foreach (var torrent in engine.Torrents.ToArray())
                    {
                        switch (torrent.State)
                        {
                            case TorrentState.Seeding:
                                await torrent.StopAsync();
                                await engine.RemoveAsync(torrent);
                                await Notify(
                                    title: $"{torrent.Torrent.Name} Finished",
                                    message: $"The Torrent '{torrent.Torrent.Name}' successfully finished downloading",
                                    url: $"https://files.conesoft.net/Downloads/Torrents/{torrent.Torrent.Name}"
                                );
                                break;
                        }
                        await engine.SaveStateAsync(stateFile.Path);
                    }
                    await Task.Delay(1000);
                }
            });
        }

        private void Engine_StatsUpdate(object? sender, StatsUpdateEventArgs e)
        {
            Update?.Invoke();
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
            await cts.CancelAsync();
            await engine.StopAllAsync();
        }

        record Link(string Url, string Name);
        record Config(string DownloadUrl, Link[] Links);

        async Task Notify(string title, string message, string url)
        {
            var query = new QueryBuilder
            {
                { "token", conesoftSecret },
                { "title", title },
                { "message", message },
                { "url", url }
            };

            await new HttpClient().GetAsync($@"https://conesoft.net/notify" + query.ToQueryString());
        }
    }
}
