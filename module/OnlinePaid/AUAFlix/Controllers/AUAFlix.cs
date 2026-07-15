using AUAFlix.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Attributes;

namespace AUAFlix;

public class AUAFlixController : BaseOnlineController<AUAFlixSettings>
{
    static readonly HttpClient httpClient = FriendlyHttp.CreateHttpClient();

    static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
    {
        Error = (se, ev) => { ev.ErrorContext.Handled = true; }
    };

    public AUAFlixController() : base(ModInit.conf.AUAFlix)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 1)
                httpHydra.RegisterHttp(httpClient);
        };

        loadKitInitialization = (j, i, c) =>
        {
            return i;
        };
    }

    [HttpGet, Staticache(manually: true)]
    [AllowAnonymous]
    [Route("lite/a-uaflix")]
    [Route("lite/auaflix")]
    async public Task<ActionResult> Index(string title, string original_title, byte clarification, short year, int postid = 0, short t = -1, short? s = null, bool rjson = false, bool similar = false, string source = null, string id = null, string imdb_id = null, string tmdb_id = null, string kinopoisk_id = null, short? serial = null)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        var oninvk = new AUAFlixInvoke(
           init, host, "lite/a-uaflix", httpHydra,
           streamfile => HostStreamProxy(streamfile), rjson: rjson
        );

        string href = null;

        #region Handle source+id (must be BEFORE postid check)
        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
        {
            if (source.Equals("a-uaflix", StringComparison.OrdinalIgnoreCase))
            {
                href = HttpUtility.UrlDecode(id);
                postid = href.GetHashCode();
            }
            else if (source.Equals("tmdb", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(tmdb_id))
            {
                tmdb_id = id;
            }
        }
        #endregion

        #region Search
        if (postid == 0)
        {

            if (postid == 0)
            {
                var search = await InvokeCacheResult(
                    $"a-uaflix:search:{title}:{original_title}:{year}:{clarification}:{similar}:{imdb_id}:{tmdb_id}:{serial}",
                    40,
                    () => oninvk.Search(title, original_title, clarification, year, similar, imdb_id, tmdb_id, kinopoisk_id, serial)
                );

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (search.Value == null || search.Value.Count == 0)
                    return OnError("no results");

                var item = search.Value[0];
                postid = item.id;
                href = item.href;

                if (search.Value.Count > 1 && !similar)
                {
                    var stpl = new SimilarTpl(search.Value.Count);
                    string enc_title = HttpUtility.UrlEncode(title);
                    string enc_original_title = HttpUtility.UrlEncode(original_title);

                    foreach (var si in search.Value)
                    {
                        string name = !string.IsNullOrEmpty(si.title) ? si.title : si.original_title;
                        string destHref = !string.IsNullOrEmpty(si.href)
                            ? $"{host}/lite/a-uaflix?source=a-uaflix&id={HttpUtility.UrlEncode(si.href)}&title={enc_title}&original_title={enc_original_title}"
                            : $"{host}/lite/a-uaflix?postid={si.id}&title={enc_title}&original_title={enc_original_title}";

                        stpl.Append(
                            name, si.year.ToString(), si.original_title, destHref,
                            !string.IsNullOrEmpty(si.poster) ? PosterApi.Size(si.poster) : null
                        );
                    }
                    return ContentTpl(stpl);
                }
                else if (similar || search.Value.Count == 0)
                {
                    var stpl = new SimilarTpl(search.Value.Count);
                    string enc_title = HttpUtility.UrlEncode(title);
                    string enc_original_title = HttpUtility.UrlEncode(original_title);

                    foreach (var si in search.Value)
                    {
                        string destHref = !string.IsNullOrEmpty(si.href)
                            ? $"{host}/lite/a-uaflix?source=a-uaflix&id={HttpUtility.UrlEncode(si.href)}&title={enc_title}&original_title={enc_original_title}"
                            : $"{host}/lite/a-uaflix?postid={si.id}&title={enc_title}&original_title={enc_original_title}";

                        stpl.Append(
                            si.title ?? si.original_title, si.year.ToString(), si.original_title, destHref,
                            !string.IsNullOrEmpty(si.poster) ? PosterApi.Size(si.poster) : null
                        );
                    }
                    return ContentTpl(stpl);
                }
            }
        }
        #endregion

        #region Content
    rhubFallback:
        var cache = await InvokeCacheResult(
            $"a-uaflix:post:{postid}:{href}", 20,
            () => oninvk.GetContent(postid, href)
        );

        if (IsRhubFallback(cache))
            goto rhubFallback;

        StreamResponse streamResp = null;

        // For movies: get stream info for quality/subtitles
        if (cache.Value?.is_movie == true)
        {
            if (cache.Value?.stream_ref != null)
            {
                streamResp = await oninvk.GetStream(cache.Value.stream_ref);
            }
            else if (cache.Value?.streams != null && cache.Value.streams.Count > 0)
            {
                var firstStream = cache.Value.streams[0];
                if (firstStream.ref_data != null)
                {
                    streamResp = await oninvk.GetStream(firstStream.ref_data);
                }
            }
        }

        return ContentTpl(cache,
            () => oninvk.Tpl(cache.Value, postid, title, original_title, t, s, streamResp: streamResp)
        );
        #endregion
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("lite/a-uaflix/stream")]
    [Route("lite/auaflix/stream")]
    async public Task<ActionResult> Stream([FromQuery(Name = "ref")] string refData)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        try
        {
            JObject refJson = JObject.Parse(HttpUtility.UrlDecode(refData));
            var streamInvoke = new AUAFlixInvoke(
                init, host, "lite/a-uaflix", httpHydra,
                streamfile => HostStreamProxy(streamfile)
            );
            var streamResp = await streamInvoke.GetStream(refJson);
            if (streamResp?.streams == null || streamResp.streams.Count == 0)
                return OnError("no stream");

            // Redirect to first quality via HostStreamProxy
            var firstStream = streamResp.streams[0];
            string firstUrl = HostStreamProxy(firstStream.url);
            return Redirect(firstUrl);
        }
        catch { return OnError("stream error"); }
    }
}
