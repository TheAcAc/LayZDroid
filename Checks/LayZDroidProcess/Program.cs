using LayZDroid;

int count=0;
void Check(bool value,string message){if(!value)throw new Exception("FAIL "+message);Console.WriteLine("PASS "+message);count++;}
var started=DateTime.UnixEpoch;
const string path=@"D:\LayZDroid\data\runtime\sdk\emulator\qemu\windows-x86_64\qemu-system-x86_64.exe";
const string command="-avd LayZDroid_exact -port 5580";
Check(ProcessInventory.Resolve(1704,"",command,()=>null) is null,"exited process with an empty WMI path does not block instance operations");
var recovered=ProcessInventory.Resolve(42,"",command,()=>new ProcessIdentity(started,path));
Check(recovered is { } row&&row.Pid==42&&row.Start==started&&row.Path==path&&row.Args==command,"live process identity supplies a missing WMI path without changing instance arguments");
Check(ProcessInventory.Resolve(42,path,command,()=>null) is null,"stale WMI row with a populated path is ignored after process exit");
Check(ProcessInventory.Resolve(42,@"C:\other\qemu-system-x86_64.exe",command,()=>new ProcessIdentity(started,path)) is null,"a replaced PID with a different executable cannot inherit stale ownership");
try{ProcessInventory.Resolve(42,"",command,()=>new ProcessIdentity(started,""));throw new Exception("FAIL unverifiable live process accepted");}catch(IOException){Check(true,"live process without a verifiable identity still blocks operations");}
Check(ProcessInventory.Resolve(42,path,command,()=>new ProcessIdentity(started,path)) is not null,"verified emulator ownership remains available");
var timedResolve=typeof(ProcessInventory).GetMethods().SingleOrDefault(m=>m.Name=="Resolve"&&m.GetParameters().Length==5);
Check(timedResolve is not null,"process inventory correlates WMI creation time with the native process identity");
object? Timed(DateTime actual,string reported)=>timedResolve!.Invoke(null,[42,path,command,(Func<ProcessIdentity?>)(()=>new ProcessIdentity(actual,path)),reported]);
Check(Timed(started.AddSeconds(1),"19700101000000.000000+000") is null,"same-executable PID reuse cannot inherit stale instance arguments");
Check(Timed(started,"19700101000000.000000+000") is not null,"matching native and WMI creation times preserve verified ownership");
Check(Timed(started,"19700101010000.000000+060") is not null,"WMI creation-time timezone offsets normalize to UTC");
try{Timed(started,"");throw new Exception("FAIL missing creation time was accepted");}catch(System.Reflection.TargetInvocationException ex)when(ex.InnerException is IOException){Check(true,"missing live WMI creation time blocks unverifiable ownership");}
Console.WriteLine($"PASS all {count} process identity regressions");
using var own=System.Diagnostics.Process.GetCurrentProcess();
var native=ProcessInventory.Inspect(own.Id);
Check(native is { } current&&current.Start==own.StartTime.ToUniversalTime()&&string.Equals(current.Path,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase),"native limited-rights query verifies the current process path and creation time");
var absent=System.Diagnostics.Process.GetProcesses().Select(p=>{using(p)return p.Id;}).Max()+10000;
Check(ProcessInventory.Inspect(absent) is null,"process that disappeared before OpenProcess is ignored");
Console.WriteLine($"PASS all {count} process identity checks");
if(args.Contains("--inventory"))
{
    foreach(var filter in new[]{"Name='adb.exe' OR Name='HD-Adb.exe'","Name='qemu-system-x86_64.exe' OR Name='emulator.exe'"})
    {
        var rows=Runner.Inventory(filter);
        Check(rows.All(row=>row.Pid>0&&!string.IsNullOrWhiteSpace(row.Path)),"production WMI inventory resolves current Windows process identities: "+rows.Count+" rows");
    }
}
