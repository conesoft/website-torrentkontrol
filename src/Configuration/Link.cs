namespace Conesoft.Website.TorrentKontrol.Configuration;

public record Link(string Url, string Name)
{
    public Link() : this("", "")
    {
    }
};