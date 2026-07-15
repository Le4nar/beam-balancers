namespace AUAFlix.Models;

public class ModuleConf
{
    public AUAFlixSettings AUAFlix { get; set; } = new AUAFlixSettings("AUAFlix", "https://bbe.lme.isroot.in/api/v2")
    {
        displayindex = 330,
        rhub_safety = false,
        streamproxy = true,
        stream_access = "apk,cors,web",
        headers = HeadersModel.Init(
            ("Accept-Encoding", "gzip")
        ).ToDictionary()
    };
}
