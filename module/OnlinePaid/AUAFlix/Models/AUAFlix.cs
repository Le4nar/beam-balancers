using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AUAFlix.Models;

public class SearchItem
{
    public string title { get; set; }
    public string title_en { get; set; }
    public string original_title { get; set; }
    public int? year { get; set; }
    public string imdb_id { get; set; }
    public long tmdb_id { get; set; }
    public string kinopoisk_id { get; set; }
    public string type { get; set; }
    public string poster { get; set; }
    [JsonProperty("ref")]
    public JObject ref_data { get; set; }
}

public class SearchResponse
{
    public bool ok { get; set; }
    public List<SearchItem> items { get; set; }
}

public class StreamInfo
{
    public string title { get; set; }
    [JsonProperty("ref")]
    public JObject ref_data { get; set; }
}

public class ContentResponse
{
    public bool ok { get; set; }
    public string source { get; set; }
    public string type { get; set; }
    public List<VoiceData> voices { get; set; }
    public JObject stream_ref { get; set; }
    public List<StreamInfo> streams { get; set; }
}

public class VoiceData
{
    public string id { get; set; }
    public string display_name { get; set; }
    public List<SeasonData> seasons { get; set; }
}

public class SeasonData
{
    public string title { get; set; }
    public int? season { get; set; }
    public List<EpisodeData> episodes { get; set; }
}

public class EpisodeData
{
    public int? episode { get; set; }
    public string title { get; set; }
    [JsonProperty("ref")]
    public JObject ref_data { get; set; }
}

public class SubtitleInfo
{
    public string lang { get; set; }
    public string url { get; set; }
}

public class StreamResponse
{
    public bool ok { get; set; }
    public string source { get; set; }
    public List<StreamItem> streams { get; set; }
}

public class StreamItem
{
    public string url { get; set; }
    public string quality { get; set; }
    public string title { get; set; }
    public List<SubtitleInfo> subtitles { get; set; }
}
