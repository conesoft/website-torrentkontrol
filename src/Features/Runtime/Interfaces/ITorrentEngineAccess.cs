using Conesoft.Website.TorrentKontrol.Features.Runtime.Client;

namespace Conesoft.Website.TorrentKontrol.Features.Runtime.Interfaces;

public interface ITorrentEngineAccess
{
    public TorrentEngine? Engine { get; }

    delegate Task UpdateEvent();
    event UpdateEvent? Update;
}