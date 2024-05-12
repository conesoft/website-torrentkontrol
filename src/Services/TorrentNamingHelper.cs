using Conesoft.Blazor.Components.Interfaces;
using System.Text.RegularExpressions;

namespace Conesoft.Website.TorrentKontrol.Services;

public partial class TorrentNamingHelper : ITagListGenerator, ICleanNameGenerator
{
    public IEnumerable<string> GenerateTagListFromString(string tagListSource) => DetailedInfo.GetFromTorrent(tagListSource).Tags;
    public string GenerateCleanNameFromString(string tagListSource) => DetailedInfo.GetFromTorrent(tagListSource).CleanName;

    static IEnumerable<string> TagMapping(string tag)
    {
        tag = tag.ToUpperInvariant();
        if (ExtractTVEpisode(tag) is TvShowEpisode episode)
        {
            yield return $"Season {episode.Season}";
            if (episode.Episode != null)
            {
                yield return $"Episode {episode.Episode}";
            }
        }
        if (tag.Contains("264") || tag.Contains("AVC"))
        {
            yield return "H.264";
        }
        if (tag.Contains("265") || tag.Contains("HEVC"))
        {
            yield return "H.265";
        }
        var converted = tag switch
        {
            "4k" or "2160P" => "2160p",
            "1080P" => "1080p",
            "720P" => "720p",
            "10BIT" or "HDR" => "HDR",
            "FLAC" => tag,
            _ => null
        };
        if (converted is not null)
        {
            yield return converted;
        }
    }

    record TvShowEpisode(int Season, int? Episode);
    static TvShowEpisode? ExtractTVEpisode(string tag)
    {
        var regex = ParseSeasonAndEpisode();
        var match = regex.Match(tag);

        if (match.Success)
        {
            return new(
                Season: int.Parse(match.Groups["Season"].Value),
                Episode: int.Parse(match.Groups["Episode"].Value)
            );
        }
        if (tag.Length > 1 && (tag[0] == 'S'))
        {
            return int.TryParse(tag[1..], out var season) ? new(Season: season, Episode: null) : null;
        }
        return null;
    }

    [GeneratedRegex(@"S(?<Season>\d{1,2})E(?<Episode>\d{1,2})")]
    private static partial Regex ParseSeasonAndEpisode();

    record DetailedInfo(string CleanName, string[] Tags)
    {
        public static DetailedInfo GetFromTorrent(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return new("", []);
            }
            var segments = name.Split((char[])[' ', '.', '[', ']', '-'], StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> tags = [];
            List<string> clean = [];
            foreach (var segment in segments)
            {
                foreach (var tag in TagMapping(segment))
                {
                    tags.Add(tag);
                }
                if (tags.Any() == false)
                {
                    clean.Add(segment);
                }
            }
            var cleanName = string.Join(" ", clean);
            return new(CleanName: string.IsNullOrWhiteSpace(cleanName) == false ? cleanName : name, Tags: [.. tags]);
        }

    }
}