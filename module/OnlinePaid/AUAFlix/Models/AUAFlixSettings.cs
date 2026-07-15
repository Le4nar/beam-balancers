using Newtonsoft.Json;

namespace AUAFlix.Models;

public class AUAFlixSettings : BaseSettings, ICloneable
{
    public AUAFlixSettings(string plugin, string host, bool enable = true)
    {
        this.enable = enable;
        this.plugin = plugin;

        if (host != null)
            this.host = host.StartsWith("http") ? host : Decrypt(host);
    }

    [JsonIgnore]
    public string token { get; set; }

    [JsonIgnore]
    public string device_id { get; set; }

    public AUAFlixSettings Clone()
    {
        return (AUAFlixSettings)MemberwiseClone();
    }

    object ICloneable.Clone()
    {
        return MemberwiseClone();
    }
}
