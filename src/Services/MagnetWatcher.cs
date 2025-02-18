using Conesoft.Files;
using Conesoft.Hosting;
using MonoTorrent;

namespace Conesoft.Website.TorrentKontrol.Services;

public class MagnetWatcher(HostEnvironment environment, Torrents torrents) : IHostedService
{
    CancellationTokenSource? cancellationTokenSource;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var source = environment.Local.Storage / "new" / "magnets";

        cancellationTokenSource = source.Live(async () =>
        {
            foreach(var file in source.Files.ToArray())
            {
                if(await file.ReadText() is string magnetLink)
                {
                    await torrents.Add(MagnetLink.Parse(Uri.UnescapeDataString(magnetLink)));
                }
                file.Delete();
            }
        }, allDirectories: false);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationTokenSource?.Cancel();

        return Task.CompletedTask;
    }
}
