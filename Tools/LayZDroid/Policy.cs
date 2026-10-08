namespace LayZDroid;
public static class Policy
{
    const long Mb=1024*1024;
    public static (bool Allowed,string Reason,long Required) CanStart(long total,long available,long commitRemaining,int ramMb,long reserved)
    {
        if(total<=0||available<0||commitRemaining<0||reserved<0||ramMb<768)return(false,"Resource measurements or settings are invalid.",0);
        long guest=checked(ramMb*Mb),need=checked(guest+Math.Max(512*Mb,guest/4));
        long hostReserve=Math.Max(1024*Mb,total/5);
        if(available-reserved-need<hostReserve)return(false,"Not enough free RAM after reserving memory for Windows. Stop an instance or close other applications.",need);
        if(commitRemaining-reserved<need)return(false,"Windows has insufficient remaining commit capacity.",need);
        return(true,"Ready",need);
    }
    public static int NextPort(ISet<int> busy)
    {
        // This pinned engine's published -help-port range; no artificial count limit.
        foreach(int p in Enumerable.Range(0,16).Select(n=>5554+2*n).OrderBy(p=>p<5580?1:0).ThenBy(p=>p))
            if(!busy.Contains(p)&&!busy.Contains(p+1))return p;
        throw new InvalidDataException("All ports supported by this preview engine are occupied.");
    }
    public static string ExtractionPath(string root,string entry)
    {
        if(string.IsNullOrWhiteSpace(entry)||entry.Contains(':')||entry.StartsWith('/')||entry.StartsWith('\\')||Path.IsPathRooted(entry))throw new InvalidDataException("Invalid archive path.");
        var normalized=entry.Replace('\\','/');
        if(normalized.Split('/').Any(p=>p==".."))throw new InvalidDataException("Archive path traversal.");
        var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var path=Path.GetFullPath(Path.Combine(root,normalized));
        if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Archive leaves its destination.");
        return path;
    }
    public static void ValidateSettings(int ram,int cores,int width,int height)
    {if(ram is <768 or >8192||cores is <1 or >16||!((width==480&&height==854)||(width==720&&height==1280)))throw new InvalidDataException("Use 768–8192 MB RAM, 1–16 cores and a supported portrait resolution.");}
    public static bool EffectiveRamMatches(int requested,int effective)=>requested==effective;
    public static int SelectAdbPort(int preferred,ISet<int> busy,Func<int,bool> trusted)
    {
        bool IsTrusted(int port){try{return trusted(port);}catch(IOException){return false;}}
        foreach(int port in new[]{preferred}.Concat(Enumerable.Range(5038,31)).Distinct())
            if(port>=5038&&port<=5068&&(!busy.Contains(port)||IsTrusted(port)))return port;
        throw new IOException("No free emulator connection is available. Close another emulator manager and try again.");
    }
    public static string[] LaunchArguments(Instance i)=>["-avd",i.AvdName,"-port",i.Port.ToString(),"-memory",i.RamMb.ToString(),"-cores",i.Cores.ToString(),"-gpu",i.Gpu,"-lowram","-no-snapshot","-no-boot-anim","-no-audio"];
    public static bool TrustedAdbListener(int pid,IEnumerable<(int Pid,string Path)> processes,string ownPath)=>processes.Any(p=>p.Pid==pid&&string.Equals(Path.GetFullPath(p.Path),Path.GetFullPath(ownPath),StringComparison.OrdinalIgnoreCase));
    public static bool Owns(int expectedPid,DateTime expectedStart,string expectedPath,int pid,DateTime start,string path)=>expectedPid==pid&&expectedStart==start&&string.Equals(Path.GetFullPath(expectedPath),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase);
}
public static class NetworkStartup
{
    public static bool IsReady(string dump)
    {
        var active=System.Text.RegularExpressions.Regex.Match(dump,@"(?m)^Active default network:\s*(\d+)");if(!active.Success)return false;
        string marker="NetworkAgentInfo{network{"+active.Groups[1].Value+"}";int start=dump.IndexOf(marker,StringComparison.Ordinal);if(start<0)return false;
        int next=dump.IndexOf("NetworkAgentInfo",start+marker.Length,StringComparison.Ordinal);string agent=next<0?dump[start..]:dump[start..next];
        if(!agent.Contains("ni{WIFI CONNECTED",StringComparison.Ordinal))return false;
        var caps=System.Text.RegularExpressions.Regex.Match(agent,@"nc\{\[\s*Transports:\s*WIFI\s+Capabilities:\s*([A-Z_&]+)");
        return caps.Success&&caps.Groups[1].Value.Split('&').Contains("VALIDATED");
    }
    public static async Task<bool> EnsureAsync(Func<CancellationToken,Task<string>> read,Func<CancellationToken,Task> reconnect,Func<CancellationToken,Task> wait,CancellationToken ct)
    {
        for(int n=0;n<3;n++){ct.ThrowIfCancellationRequested();if(IsReady(await read(ct)))return true;await wait(ct);}
        await reconnect(ct);
        for(int n=0;n<10;n++){ct.ThrowIfCancellationRequested();if(IsReady(await read(ct)))return true;await wait(ct);}
        return false;
    }
}
