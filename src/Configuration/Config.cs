namespace Conesoft.Website.TorrentKontrol.Configuration;

record Config(string DownloadUrl, Link[] Links)
{
    public Config() : this("", [])
    {
    }
}