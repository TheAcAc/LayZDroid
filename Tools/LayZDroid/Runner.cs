using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
namespace LayZDroid;
public sealed class Runner(Store store)
{
    public string Adb=>Path.Combine(store.Sdk,"platform-tools","adb.exe");
    public string Emulator=>Path.Combine(store.Sdk,"emulator","emulator.exe");
    readonly SemaphoreSlim gate=new(1,1);
    public static HashSet<int> BusyPorts()=>IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(p=>p.Port).ToHashSet();
    public ProcessStartInfo Info(string exe,IEnumerable<string> args)
    {
        var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(exe)!};foreach(var a in args)info.ArgumentList.Add(a);
        info.Environment["ANDROID_SDK_ROOT"]=store.Sdk;info.Environment["ANDROID_HOME"]=store.Sdk;info.Environment["ANDROID_AVD_HOME"]=store.Avds;
        info.Environment["ANDROID_ADB_SERVER_PORT"]="5038";info.Environment["ADB_SERVER_SOCKET"]="tcp:127.0.0.1:5038";return info;
    }
    public async Task<string> Command(string exe,IEnumerable<string> args,CancellationToken ct,int seconds=30)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(seconds));using var p=Process.Start(Info(exe,args))??throw new IOException("Cannot start command.");
        var output=p.StandardOutput.ReadToEndAsync(deadline.Token);var error=p.StandardError.ReadToEndAsync(deadline.Token);
        try{await p.WaitForExitAsync(deadline.Token);var text=await output;var errors=await error;if(p.ExitCode!=0)throw new IOException((text+errors).Trim());return text;}
        finally{if(!p.HasExited)p.Kill(false);}
    }
    public async Task EnsureAdb(CancellationToken ct)
    {
        if(BusyPorts().Contains(5038))
        {
            // Never let a differing CLI binary automatically restart another application's server.
            var owners=await Task.Run(()=>Inventory("Name='adb.exe' OR Name='HD-Adb.exe'"),ct);
            var pid=TcpOwnership.ListenerPid(5038);
            if(!Policy.TrustedAdbListener(pid,owners.Select(p=>(p.Pid,p.Path)),Adb))throw new IOException("Port 5038 is already in use by another ADB installation. Close that server yourself before using LayZDroid; it will not be stopped automatically.");
        }
        await Command(Adb,["-P","5038","start-server"],ct);
    }
    public async Task Start(Instance i,IProgress<string> progress,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        (int Pid,DateTime Start,string Path)? launched=null;
        try
        {
            if(!RuntimeSetup.Ready(store))throw new IOException("Set up the runtime first.");
            if(await Owner(i,ct) is not null)throw new IOException("This instance is already running.");
            var hardware=HostMemory.Read();var admission=Policy.CanStart(hardware.Total,hardware.Available,hardware.CommitRemaining,i.RamMb,0);if(!admission.Allowed)throw new IOException(admission.Reason);
            var accel=await Command(Emulator,["-accel-check"],ct);if(!accel.Contains("WHPX",StringComparison.OrdinalIgnoreCase))throw new IOException("Windows Hypervisor Platform is required. Enable CPU virtualization in BIOS and Windows Hypervisor Platform in Windows features, then restart Windows.");
            await EnsureAdb(ct);var busy=BusyPorts();if(busy.Contains(i.Port)||busy.Contains(i.Port+1))throw new IOException("This instance's console or ADB port is occupied.");
            store.Configure(i);i.Status="Starting";progress.Report(i.Name+": booting Android");
            var logs=Path.Combine(store.Root,"diagnostics");Directory.CreateDirectory(logs);
            var info=Info(Emulator,Policy.LaunchArguments(i));
            var p=Process.Start(info)??throw new IOException("Emulator did not start.");
            launched=(p.Id,p.StartTime.ToUniversalTime(),Emulator);i.Pid=launched.Value.Pid;i.Started=launched.Value.Start;i.ProcessPath=launched.Value.Path;
            _=CaptureLog(p,Path.Combine(logs,i.Id+".log"));
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromMinutes(3));
            while(true)
            {
                deadline.Token.ThrowIfCancellationRequested();var owner=await Owner(i,deadline.Token);
                if(owner is not null){i.Pid=owner.Value.Pid;i.Started=owner.Value.Start;i.ProcessPath=owner.Value.Path;}
                try{if(owner is not null&&(await Command(Adb,["-P","5038","-s","emulator-"+i.Port,"shell","getprop","sys.boot_completed"],deadline.Token,8)).Trim()=="1")break;}catch(IOException){}
                if(p.HasExited&&owner is null)throw new IOException("Emulator exited. See diagnostics for details.");
                await Task.Delay(1500,deadline.Token);
            }
            var hw=Path.Combine(store.InstancePath(i),"hardware-qemu.ini");
            if(!File.Exists(hw))throw new IOException("Cannot verify effective emulator RAM.");
            var match=Regex.Match(File.ReadAllText(hw),@"(?m)^hw\.ramSize\s*=\s*(\d+)");
            if(!match.Success||!Policy.EffectiveRamMatches(i.RamMb,int.Parse(match.Groups[1].Value)))throw new IOException("The engine changed the requested RAM allocation. Review settings before treating this instance as low-memory.");
            i.Status="Ready";progress.Report(i.Name+": ready");
        }
        catch(Exception failure)
        {
            if(launched is not null)
            {
                using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try
                {
                    await CleanupLaunchedAsync(i,launched.Value,cleanup.Token);
                }
                catch(Exception cleanupFailure){i.Status="Needs attention";throw new IOException(failure.Message+" Cleanup needs review: "+cleanupFailure.Message,failure);}
            }
            i.Status="Needs attention";throw;
        }
        finally{gate.Release();}
    }
    public async Task CleanupLaunchedAsync(Instance i,(int Pid,DateTime Start,string Path) launched,CancellationToken ct)
    {
                    try{using var launcher=Process.GetProcessById(launched.Pid);if(!launcher.HasExited&&Policy.Owns(launched.Pid,launched.Start,launched.Path,launcher.Id,launcher.StartTime.ToUniversalTime(),launcher.MainModule?.FileName??""))launcher.Kill(false);}catch(ArgumentException){}
                    for(int attempt=0;attempt<3;attempt++){await Task.Delay(500,ct);var owned=await Owner(i,ct);if(owned is null)continue;if(owned.Value.Start<launched.Start)throw new IOException("Cleanup ownership predates this launch.");i.Pid=owned.Value.Pid;i.Started=owned.Value.Start;i.ProcessPath=owned.Value.Path;using var vm=Process.GetProcessById(i.Pid);if(Policy.Owns(i.Pid,i.Started,i.ProcessPath,vm.Id,vm.StartTime.ToUniversalTime(),vm.MainModule?.FileName??"")){vm.Kill(false);await vm.WaitForExitAsync(ct);}}
                    i.Pid=0;
    }
    static async Task CaptureLog(Process p,string path)
    {
        try
        {
            using var output=new StreamWriter(path,false);using var writeGate=new SemaphoreSlim(1);long written=0;
            async Task Pump(StreamReader reader){string? line;while((line=await reader.ReadLineAsync()) is not null){await writeGate.WaitAsync();try{if(written<4*1024*1024){await output.WriteLineAsync(line);await output.FlushAsync();written+=line.Length+1;}}finally{writeGate.Release();}}}
            await Task.WhenAll(Pump(p.StandardOutput),Pump(p.StandardError));
        }catch{}
    }
    public async Task Stop(Instance i,CancellationToken ct)
    {
        await gate.WaitAsync(ct);try
        {
            var owner=await Owner(i,ct);if(owner is null){i.Status="Stopped";i.Pid=0;return;}
            if(i.Pid==0||!Policy.Owns(i.Pid,i.Started,i.ProcessPath,owner.Value.Pid,owner.Value.Start,owner.Value.Path))throw new IOException("Instance ownership changed; stopping it was refused.");
            using var p=Process.GetProcessById(owner.Value.Pid);if(p.StartTime.ToUniversalTime()!=owner.Value.Start)throw new IOException("Instance process identity changed.");
            if(p.CloseMainWindow()){try{await p.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(10),ct);}catch(TimeoutException){}}
            if(!p.HasExited){var current=await Owner(i,ct);if(current is null||!Policy.Owns(owner.Value.Pid,owner.Value.Start,owner.Value.Path,current.Value.Pid,current.Value.Start,current.Value.Path))throw new IOException("Instance ownership changed.");p.Kill(false);await p.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(10),ct);}
            i.Status="Stopped";i.Pid=0;
        }finally{gate.Release();}
    }
    public async Task<(int Pid,DateTime Start,string Path)?> Owner(Instance i,CancellationToken ct)
    {
        var rows=await Task.Run(()=>Inventory("Name='qemu-system-x86_64.exe' OR Name='emulator.exe'"),ct);
        var matches=rows.Where(p=>p.Path.StartsWith(Path.GetDirectoryName(Emulator)!+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(p.Args,@"(?:^|\s)-avd\s+\""?"+Regex.Escape(i.AvdName)+@"\""?(?:\s|$)")&&Regex.IsMatch(p.Args,@"(?:^|\s)-port\s+"+i.Port+@"(?:\s|$)")).ToArray();
        var vm=matches.Where(p=>Path.GetFileName(p.Path)=="qemu-system-x86_64.exe").ToArray();
        if(vm.Length==1&&matches.Length<=2)return(vm[0].Pid,vm[0].Start,vm[0].Path);
        if(matches.Length>1)throw new IOException("Multiple matching emulator owners; manual review required.");
        return matches.Length==0?null:(matches[0].Pid,matches[0].Start,matches[0].Path);
    }
    public static List<(int Pid,DateTime Start,string Path,string Args)> Inventory(string filter)
    {
        var result=new List<(int,DateTime,string,string)>();
        var type=Type.GetTypeFromProgID("WbemScripting.SWbemLocator")??throw new IOException("Windows process inventory is unavailable.");
        dynamic locator=Activator.CreateInstance(type)!;dynamic services=locator.ConnectServer(".","root\\cimv2");dynamic rows=services.ExecQuery("SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process WHERE "+filter,"WQL",0);
        try{foreach(dynamic row in rows){try{int pid=(int)(uint)row.ProcessId;using var p=Process.GetProcessById(pid);if(p.HasExited)continue;string path=(string?)row.ExecutablePath??throw new IOException("Process path unavailable.");result.Add((pid,p.StartTime.ToUniversalTime(),path,(string?)row.CommandLine??""));}catch(ArgumentException){}}}
        finally{Marshal.FinalReleaseComObject(rows);Marshal.FinalReleaseComObject(services);Marshal.FinalReleaseComObject(locator);}
        return result;
    }
    public async Task OpenGame(Instance i,CancellationToken ct)
    {
        if(await Owner(i,ct) is null)throw new IOException("Start this instance first.");await EnsureAdb(ct);
        if(string.IsNullOrEmpty(i.Package))throw new IOException("Import the KaW APK or set its package first.");
        var component=(await Command(Adb,["-P","5038","-s","emulator-"+i.Port,"shell","cmd","package","resolve-activity","--brief",i.Package],ct)).Trim().Split('\n').Last();
        if(!component.Contains('/')||component.Any(char.IsWhiteSpace))throw new IOException("No launchable game activity found. Check the installed APK.");
        await Command(Adb,["-P","5038","-s","emulator-"+i.Port,"shell","am","start","-n",component],ct);
    }
    public async Task Import(Instance i,string[] apks,CancellationToken ct)
    {
        if(await Owner(i,ct) is null)throw new IOException("Start this instance before importing APKs.");
        if(apks.Length==0)throw new IOException("Select the base APK and its required splits.");
        var metadata=apks.Select(ApkMetadata.Read).ToArray();var package=metadata[0].Package;
        if(metadata.Any(m=>m.Package!=package||m.Version!=metadata[0].Version))throw new IOException("APK splits must have the same package and version.");
        if(metadata.Any(m=>m.MinimumApi>34))throw new IOException("This APK needs a newer Android version than this preview.");
        var abis=metadata.SelectMany(m=>m.Abis).Distinct().ToArray();if(abis.Length>0&&!abis.Contains("x86_64"))throw new IOException("This APK requires ARM native libraries. This preview uses x86_64 and has no ARM translation layer.");
        await EnsureAdb(ct);var args=new List<string>{"-P","5038","-s","emulator-"+i.Port,apks.Length==1?"install":"install-multiple","-r"};args.AddRange(apks);
        await Command(Adb,args,ct,120);i.Package=package;
    }
}
public static class HostMemory
{
    [StructLayout(LayoutKind.Sequential)]struct Memory{public uint Length,Load;public ulong TotalPhys,AvailPhys,TotalPage,AvailPage,TotalVirtual,AvailVirtual,AvailExtended;}
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref Memory data);
    public static (long Total,long Available,long CommitRemaining) Read(){var m=new Memory{Length=(uint)Marshal.SizeOf<Memory>()};if(!GlobalMemoryStatusEx(ref m))throw new IOException("Cannot read host memory.");return((long)m.TotalPhys,(long)m.AvailPhys,(long)m.AvailPage);}
}
public static class TcpOwnership
{
    [DllImport("iphlpapi.dll",SetLastError=true)]static extern uint GetExtendedTcpTable(IntPtr table,ref int size,bool order,int family,int tableClass,uint reserved);
    public static int ListenerPid(int port)
    {
        int size=0;uint result=GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,3,0);if(result!=122||size<4)throw new IOException("Cannot inspect ADB listener ownership.");
        var memory=Marshal.AllocHGlobal(size);try{result=GetExtendedTcpTable(memory,ref size,false,2,3,0);if(result!=0)throw new IOException("Cannot inspect ADB listener ownership.");int count=Marshal.ReadInt32(memory);if(count<0||4L+24L*count>size)throw new IOException("Invalid TCP ownership table.");var matches=new List<int>();for(int n=0;n<count;n++){int offset=4+n*24;uint raw=(uint)Marshal.ReadInt32(memory,offset+8);int actual=(int)(((raw&255)<<8)|((raw>>8)&255));if(actual==port)matches.Add(Marshal.ReadInt32(memory,offset+20));}return matches.Distinct().Count()==1?matches[0]:throw new IOException("ADB listener ownership is absent or ambiguous.");}finally{Marshal.FreeHGlobal(memory);}
    }
}
