using Conesoft.Hosting;
using Conesoft.Website.TorrentKontrol.Features.Runtime;
using Conesoft.Website.TorrentKontrol.Features.Runtime.Interfaces;
using MonoTorrent;

namespace Conesoft.Website.TorrentKontrol.Features.Watchers.Services;

public class MagnetWatcher(HostEnvironment environment, ITorrentEngineAccess engineAccess) : BackgroundEntryWatcher<Files.Directory>
{
    protected override Task<Files.Directory> GetEntry() => Task.FromResult(environment.Local.Storage / "new" / "magnets");

    public override async Task OnChange(Files.Directory entry)
    {
        if (engineAccess.Engine is TorrentEngine engine)
        {
            foreach (var file in entry.Files)
            {
                if (await file.ReadText() is string magnetLink)
                {
                    await engine.Add(MagnetLink.Parse(Uri.UnescapeDataString(magnetLink)));
                }
                await file.Delete();
            }
        }
    }
}