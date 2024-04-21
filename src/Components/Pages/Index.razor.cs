using System.Text.RegularExpressions;

namespace Conesoft.Website.TorrentKontrol.Components.Pages;
public partial class Index
{
    static IEnumerable<string> TagMapping(string tag)
    {
        if (ExtractTVEpisode(tag) is TvShowEpisode episode)
        {
            yield return $"Season {episode.Season}";
            yield return $"Episode {episode.Episode}";
        }
        if(tag.Contains("264") || tag.Contains("AVC"))
        {
            yield return "H.264";
        }
        if(tag.Contains("265") || tag.Contains("HEVC"))
        {
            yield return "H.265";
        }
        var converted = tag switch
        {
            "4k" or "2160p" => "2160p",
            "1080p" => tag,
            "720p" => tag,
            "10bit" or "HDR" => "HDR",
            _ => null
        };
        if (converted is not null)
        {
            yield return converted;
        }
    }

    record TvShowEpisode(int Season, int Episode);
    static TvShowEpisode? ExtractTVEpisode(string tag)
    {
        var regex = ParseSeasonAndEpisode();
        var match = regex.Match(tag.ToUpper());

        return match.Success ? new(
                Season: int.Parse(match.Groups["Season"].Value),
                Episode: int.Parse(match.Groups["Episode"].Value)
            ) : null;
    }

    [GeneratedRegex(@"S(?<Season>\d{1,2})E(?<Episode>\d{1,2})")]
    private static partial Regex ParseSeasonAndEpisode();
}
