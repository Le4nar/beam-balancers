namespace AFilmix.Models;

public class ModuleConf
{
    public AFilmixSettings AFilmix { get; set; } = new AFilmixSettings("AFilmix", "https://bbe.lme.isroot.in/api/v2")
    {
        displayindex = 325,
        rhub_safety = false,
        streamproxy = true,
        stream_access = "apk,cors,web",
        headers = HeadersModel.Init(
            ("Accept-Encoding", "gzip")
        ).ToDictionary()
    };
}
