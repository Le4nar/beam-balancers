using AUAFlix.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Templates;
using System.Text;

namespace AUAFlix;

public class AUAFlixInvoke
{
    #region static
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext<AUAFlixInvoke>();
    static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
    {
        Error = (se, ev) => { ev.ErrorContext.Handled = true; }
    };
    #endregion

    AUAFlixSettings init;
    string host, route;
    string apihost;
    Func<string, string> onstreamfile;
    bool rjson;
    HttpHydra httpHydra;

    public AUAFlixInvoke(AUAFlixSettings init, string host, string route, HttpHydra httpHydra, Func<string, string> onstreamfile, bool rjson = false)
    {
        this.init = init;
        apihost = init.host;
        this.route = route;
        this.host = host != null ? $"{host}/" : null;
        this.onstreamfile = onstreamfile;
        this.rjson = rjson;
        this.httpHydra = httpHydra;
    }

    #region Search
    async public Task<List<SearchResultItem>> Search(string title, string original_title, byte clarification, short year, bool similar, string imdb_id = null, string tmdb_id = null, string kinopoisk_id = null, short? serial = null)
    {
        if (string.IsNullOrWhiteSpace(title ?? original_title) && string.IsNullOrWhiteSpace(imdb_id) && string.IsNullOrWhiteSpace(tmdb_id))
            return null;

        string query = null;
        if (!string.IsNullOrWhiteSpace(title ?? original_title))
        {
            query = clarification == 1 ? title : (original_title ?? title);
            if (string.IsNullOrWhiteSpace(query))
                query = title ?? original_title;
        }

        var uri = new StringBuilder($"{apihost}/search?source=uaflix&sources=uaflix");
        if (!string.IsNullOrEmpty(query))
            uri.Append($"&title={HttpUtility.UrlEncode(query)}");
        if (!string.IsNullOrEmpty(original_title))
            uri.Append($"&original_title={HttpUtility.UrlEncode(original_title)}");
        if (!string.IsNullOrEmpty(imdb_id))
            uri.Append($"&imdb_id={imdb_id}");
        if (!string.IsNullOrEmpty(tmdb_id))
            uri.Append($"&tmdb_id={tmdb_id}");
        if (!string.IsNullOrEmpty(kinopoisk_id))
            uri.Append($"&kinopoisk_id={kinopoisk_id}");
        if (year > 0)
            uri.Append($"&year={year}");
        if (serial.HasValue)
            uri.Append($"&serial={serial.Value}");

        var json = await httpHydra.Get(uri.ToString(), useDefaultHeaders: false, safety: false);
        if (json == null)
            return null;

        try
        {
            var response = JsonConvert.DeserializeObject<SearchResponse>(json, jsonSettings);
            if (response?.ok != true || response.items == null || response.items.Count == 0)
                return null;

            var results = new List<SearchResultItem>(response.items.Count);

            foreach (var item in response.items)
            {
                if (item == null)
                    continue;

                int itemId = 0;
                string href = null;
                string type = item.type;

                if (item.ref_data != null)
                {
                    if (item.ref_data["id"] != null)
                        int.TryParse(item.ref_data["id"]?.ToString(), out itemId);
                    if (item.ref_data["tmdb_id"] != null)
                        int.TryParse(item.ref_data["tmdb_id"]?.ToString(), out itemId);
                    if (item.ref_data["href"] != null)
                        href = item.ref_data["href"]?.ToString();
                }

                if (itemId == 0 && href == null)
                    continue;

                results.Add(new SearchResultItem
                {
                    id = itemId > 0 ? itemId : href.GetHashCode(),
                    href = href,
                    title = item.title,
                    original_title = item.original_title ?? item.title_en,
                    year = item.year ?? 0,
                    poster = item.poster,
                    type = type ?? "movie"
                });
            }

            return results;
        }
        catch { return null; }
    }
    #endregion

    #region GetContent
    async public Task<ContentData> GetContent(int postid, string href = null)
    {
        string refJson;
        if (!string.IsNullOrEmpty(href))
            refJson = $"{{\"href\":\"{href}\"}}";
        else
            refJson = $"{{\"id\":{postid},\"tmdb_id\":{postid}}}";

        string body = $"{{\"source\":\"uaflix\",\"ref\":{refJson},\"full\":true}}";
        string uri = $"{apihost}/content";

        string json = await httpHydra.Post(uri, body, safety: false, addheaders: HeadersModel.Init(
            ("Content-Type", "application/json")
        ));

        if (json == null)
            return null;

        try
        {
            var response = JsonConvert.DeserializeObject<ContentResponse>(json, jsonSettings);
            if (response?.ok != true)
                return null;

            string streamUrl = null;
            if (response.stream_ref != null && response.stream_ref["url"] != null)
                streamUrl = response.stream_ref["url"].ToString();

            return new ContentData
            {
                type = response.type,
                voices = response.voices,
                is_movie = response.type == "movie",
                stream_ref = response.stream_ref,
                streams = response.streams,
                content_href = href
            };
        }
        catch { return null; }
    }

    async public Task<StreamResponse> GetStream(JObject streamRef)
    {
        string refStr = streamRef.ToString(Formatting.None);
        string body = $"{{\"source\":\"uaflix\",\"ref\":{refStr}}}";
        string uri = $"{apihost}/stream";

        string json = await httpHydra.Post(uri, body, safety: false, addheaders: HeadersModel.Init(
            ("Content-Type", "application/json")
        ));

        if (json == null)
            return null;

        try
        {
            return JsonConvert.DeserializeObject<StreamResponse>(json, jsonSettings);
        }
        catch { return null; }
    }
    #endregion

    #region Html
    public ITplResult Tpl(ContentData data, int postid, string title, string original_title, short t, short? s, StreamResponse streamResp = null)
    {
        if (data == null)
            return default;

        if (data.is_movie)
        {
            #region Фильм
            var mtpl = new MovieTpl(title, original_title);

            // Priority 1: Use streams from content response — route through stream endpoint
            if (data.streams != null && data.streams.Count > 0)
            {
                foreach (var stream in data.streams)
                {
                    // Use stream endpoint URL instead of direct CDN URL
                    string streamId = HttpUtility.UrlEncode(stream.ref_data?.ToString(Formatting.None) ?? "{}");
                    string streamLink = $"{host}{route}/stream?ref={streamId}";
                    string voiceName = stream.title ?? "Original";

                    var sq = new StreamQualityTpl();
                    sq.Append(streamLink, "auto");

                    mtpl.Append(voiceName, streamLink, streamquality: sq);
                }
            }
            // Priority 2: Use stream_ref for single stream endpoint URL
            else if (data.stream_ref != null || (streamResp?.streams != null && streamResp.streams.Count > 0))
            {
                JObject refForStream;
                if (data.stream_ref != null)
                    refForStream = data.stream_ref;
                else
                    refForStream = streamResp.streams[0]?.url != null
                        ? new JObject { ["url"] = streamResp.streams[0].url, ["title"] = streamResp.streams[0].title ?? "Original" }
                        : new JObject { ["url"] = "" };

                string streamId = HttpUtility.UrlEncode(refForStream.ToString(Formatting.None));
                string streamLink = $"{host}{route}/stream?ref={streamId}";

                var sq = new StreamQualityTpl();
                sq.Append(streamLink, "auto");

                string voiceName = data.stream_ref?["voice"]?.ToString() ?? streamResp?.streams?[0]?.title ?? "Original";
                mtpl.Append(voiceName, streamLink, streamquality: sq);
            }
            // Priority 3: Fallback to voice/episode data
            else if (data.voices != null)
            {
                foreach (var voice in data.voices)
                {
                    if (voice.seasons == null || voice.seasons.Count == 0)
                        continue;

                    foreach (var season in voice.seasons)
                    {
                        if (season.episodes == null || season.episodes.Count == 0)
                            continue;

                        foreach (var episode in season.episodes)
                        {
                            string streamId = HttpUtility.UrlEncode(episode.ref_data?.ToString(Formatting.None) ?? "{}");
                            string streamLink = $"{host}{route}/stream?ref={streamId}";

                            var sq = new StreamQualityTpl();
                            sq.Append(streamLink, "auto");

                            mtpl.Append(
                                voice.display_name ?? "Original",
                                streamLink,
                                streamquality: sq
                            );
                        }
                    }
                }
            }

            return mtpl;
            #endregion
        }
        else
        {
            #region Сериал
            if (data.voices == null || data.voices.Count == 0)
                return default;

            var voice = data.voices[t >= 0 && t < data.voices.Count ? t : 0];
            if (voice == null)
                return default;

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            if (s == null)
            {
                #region Сезоны
                // Collect all seasons from all voices
                var allSeasons = new List<SeasonData>();
                foreach (var v in data.voices)
                {
                    if (v.seasons != null)
                        allSeasons.AddRange(v.seasons);
                }
                var distinctSeasons = allSeasons.DistinctBy(s => s.title).OrderBy(s => s.title).ToList();
                if (distinctSeasons.Count == 0)
                    return default;
                var tpl = new SeasonTpl(distinctSeasons.Count);

                foreach (var season in distinctSeasons)
                {
                    tpl.Append(
                        $"{season.title ?? season.season?.ToString()} сезон",
                        $"{host}{route}?source=a-uaflix&id={HttpUtility.UrlEncode(data.content_href ?? "https://uafix.net/")}&rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&s={season.title ?? season.season?.ToString()}",
                        season.title ?? season.season?.ToString()
                    );
                }

                return tpl;
                #endregion
            }
            else
            {
                #region Перевод
                string enc_id = HttpUtility.UrlEncode(data.content_href ?? "https://uafix.net/");
                string sStr = s.Value.ToString();
                var voicesWithSeason = data.voices
                    .Where(v => v.seasons != null && v.seasons.Any(se => se.title == sStr))
                    .ToList();

                if (voicesWithSeason.Count == 0)
                    return default;

                int indexTranslate = 0;
                var vtpl = new VoiceTpl(voicesWithSeason.Count);

                foreach (var voiceItem in voicesWithSeason)
                {
                    string voiceLink = host + $"{route}?source=a-uaflix&id={enc_id}&rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&s={s}&t={indexTranslate}";
                    bool active = t == indexTranslate;

                    indexTranslate++;
                    vtpl.Append(
                        voiceItem.display_name ?? "Unknown",
                        active,
                        voiceLink
                    );
                }
                #endregion

                #region Серии
                int selectedIndex = t >= 0 && t < voicesWithSeason.Count ? t : 0;
                var selectedVoice = voicesWithSeason[selectedIndex];

                var currentSeason = selectedVoice.seasons.FirstOrDefault(se => se.title == sStr);
                if (currentSeason?.episodes == null || currentSeason.episodes.Count == 0)
                    return default;

                var episodes = currentSeason.episodes;
                var etpl = new EpisodeTpl(vtpl, episodes.Count);

                foreach (var episode in episodes)
                {
                    string streamId = HttpUtility.UrlEncode(episode.ref_data?.ToString(Formatting.None) ?? "{}");
                    string streamLink = $"{host}{route}/stream?ref={streamId}";

                    string epNum = Regex.Match(episode.title ?? "", @"(\d+)").Groups[1].Value
                        ?? episode.ref_data?["episode"]?.ToString()
                        ?? episode.episode?.ToString();

                    string epId = episode.ref_data?["episode"]?.ToString()
                        ?? Regex.Match(episode.title ?? "", @"(\d+)").Groups[1].Value
                        ?? episode.episode?.ToString();

                    // Extract quality from HLS URL: extract 1080p, 720p etc from URL
                    var sq = new StreamQualityTpl();
                    string episodeUrl = episode.ref_data?["url"]?.ToString();
                    if (!string.IsNullOrEmpty(episodeUrl))
                    {
                        var qualityMatch = Regex.Match(episodeUrl, @"(\d+p)");
                        if (qualityMatch.Success)
                            sq.Append(streamLink, qualityMatch.Groups[1].Value);
                    }
                    if (sq.IsEmpty)
                        sq.Append(streamLink, "auto");

                    etpl.Append(
                        $"{epNum} серия ({selectedVoice.display_name ?? "Unknown"})",
                        episode.title ?? title ?? original_title,
                        s.Value,
                        epId,
                        streamLink,
                        streamquality: sq
                    );
                }

                return etpl;
                #endregion
            }
            #endregion
        }
    }
    #endregion

    #region Models
    public class SearchResultItem
    {
        public int id { get; set; }
        public string href { get; set; }
        public string title { get; set; }
        public string original_title { get; set; }
        public int year { get; set; }
        public string poster { get; set; }
        public string type { get; set; }
    }

    public class ContentData
    {
        public string type { get; set; }
        public List<VoiceData> voices { get; set; }
        public bool is_movie { get; set; }
        public JObject stream_ref { get; set; }
        public string content_href { get; set; }
        public List<StreamInfo> streams { get; set; }
    }
    #endregion
}
