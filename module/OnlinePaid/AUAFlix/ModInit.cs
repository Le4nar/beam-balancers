using AUAFlix.Models;
using Microsoft.AspNetCore.Http;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Interfaces;

namespace AUAFlix;

public class ModInit : IModuleLoaded, IModuleOnline, IModuleOnlineSpider
{
    public static ModuleConf conf;

    public List<ModuleOnlineItem> Invoke(HttpContext httpContext, RequestModel requestInfo, string host, OnlineEventsModel args)
    {
        return new List<ModuleOnlineItem>(1)
        {
            new(conf.AUAFlix)
        };
    }

    public List<ModuleOnlineSpiderItem> Spider(HttpContext httpContext, RequestModel requestInfo, string host, OnlineSpiderModel args)
    {
        return new List<ModuleOnlineSpiderItem>()
        {
            new(conf.AUAFlix)
        };
    }

    public void Loaded(InitspaceModel baseconf)
    {
        CoreInit.conf.online.with_search.Add("a-uaflix");

        updateConf();
        EventListener.UpdateInitFile += updateConf;
    }

    public void Dispose()
    {
        EventListener.UpdateInitFile -= updateConf;
    }

    void updateConf()
    {
        conf = ModuleInvoke.DeserializeInit(new ModuleConf());
    }
}
