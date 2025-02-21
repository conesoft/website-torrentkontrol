using Conesoft.Hosting;
using Conesoft.Notifications;
using Conesoft.Website.TorrentKontrol.Configuration;
using Conesoft.Website.TorrentKontrol.Features.Runtime.Interfaces;
using Microsoft.Extensions.Options;
using MonoTorrent.Client;
using Serilog;

namespace Conesoft.Website.TorrentKontrol.Features.Runtime.Services;

public class TorrentService(IOptions<Config> config, HostEnvironment environment, Notifier notifier) : IHostedService, ITorrentEngineAccess
{
    readonly Files.Directory downloadFolder = Files.Directory.From(config.Value.DownloadUrl);

    TorrentEngine? engine;

    TorrentEngine? ITorrentEngineAccess.Engine => engine;
    event ITorrentEngineAccess.UpdateEvent? UpdateEvent;

    event ITorrentEngineAccess.UpdateEvent? ITorrentEngineAccess.Update
    {
        add => UpdateEvent += value;
        remove => UpdateEvent -= value;
    }

    private async Task ProcessChanges()
    {
        if (engine != null)
        {
            try
            {
                foreach (var torrent in await engine.CleanupSeeds())
                {
                    await notifier.Notify(
                        title: $"{torrent.Torrent?.Name ?? ""} Finished",
                        message: $"The Torrent '{torrent.Torrent?.Name ?? ""}' successfully finished downloading",
                        url: $"https://files.conesoft.net/Downloads/Torrents/{torrent.Torrent?.Name ?? ""}"
                    );
                }

                var torrents = engine.Torrents.Where(t => t.State == TorrentState.Downloading || t.State == TorrentState.Starting).ToArray();
                Log.Information("active torrents: {torrents}", torrents);
                Log.Information("file entries:    {entries}", downloadFolder.Filtered("", false).Select(e => e.Name));
                foreach (var entry in downloadFolder.Filtered("", false))
                {
                    entry.Visible = !torrents.Any(t => t.Torrent?.Name == entry.Name);
                }
            }
            catch (Exception exception)
            {
                Log.Error("error processing: {ex}", exception);
            }
        }

        await (UpdateEvent?.Invoke() ?? Task.CompletedTask);
    }
    private async void Engine_StatsUpdate(object? sender, StatsUpdateEventArgs e) => await ProcessChanges();

    private async void Engine_CriticalException(object? sender, CriticalExceptionEventArgs e)
    {
        Log.Error("Critical Exception in Torrents {ex}", e.Exception);
        await StopAsync(CancellationToken.None);
        await StartAsync(CancellationToken.None);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Information("starting torrent service");
        var _ = Task.Run(async () =>
        {
            engine = await TorrentEngine.StartEngine(config, environment, e =>
            {
                e.CriticalException += Engine_CriticalException;
                e.StatsUpdate += Engine_StatsUpdate;
            });

            Log.Information("torrent service started, engine {enginestatus}", engine != null ? "running" : "not running");
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("stopping torrent service");
        if (engine != null)
        {
            //timer?.Dispose();
            await (engine as IAsyncDisposable).DisposeAsync();
            //timer = null;
            engine = null;

            await (UpdateEvent?.Invoke() ?? Task.CompletedTask);
        }
        Log.Information("torrent service stopped");
    }
}