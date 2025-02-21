namespace Conesoft.Website.TorrentKontrol.Configuration;

public record Config(string DownloadUrl, Search Search, Link[] Links, string[] Trackers)
{
    public Config() : this("", new("", []), [], [])
    {
    }
}