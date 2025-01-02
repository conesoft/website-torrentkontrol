namespace Conesoft.Website.TorrentKontrol.Configuration;

record Link(string Url, string Name)
{
    public Link() : this("", "")
    {
    }
};
