namespace Conesoft.Website.TorrentKontrol.Configuration;

record Config(string DownloadUrl, Search Search, Link[] Links)
{
    public Config() : this("", new("", []), [])
    {
    }
}

record Search(string Query, Dictionary<string, string> Headers);