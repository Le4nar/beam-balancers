using Newtonsoft.Json;
using Shared.Services.Utilities;

namespace AFilmix.Models;

public class AFilmixSettings : BaseSettings, ICloneable
{
    public AFilmixSettings(string plugin, string host, bool enable = true)
    {
        this.enable = enable;
        this.plugin = plugin;

        if (host != null)
            this.host = host.StartsWith("http") ? host : Decrypt(host);
    }

    public string token { get; set; }

    [JsonIgnore]
    public string device_id { get; set; } = UnicTo.Code(16);

    public AFilmixSettings Clone()
    {
        return (AFilmixSettings)MemberwiseClone();
    }

    object ICloneable.Clone()
    {
        return MemberwiseClone();
    }
}
