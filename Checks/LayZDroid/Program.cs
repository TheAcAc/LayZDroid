using LayZDroid;
int count=0;
void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
void Reject(Action action,string name){try{action();throw new Exception("Accepted unsafe input: "+name);}catch(InvalidDataException){Check(true,name);}}
const string healthyNetwork="Active default network: 101\nNetworkAgentInfo{network{101} ni{WIFI CONNECTED extra: } nc{[ Transports: WIFI Capabilities: INTERNET&TRUSTED&VALIDATED&NOT_VPN ]}}";
const string staleNetwork="Active default network: 102\nNetworkAgentInfo{network{101} ni{WIFI CONNECTED extra: } nc{[ Transports: WIFI Capabilities: INTERNET&VALIDATED ]}}\nNetworkAgentInfo{network{102} ni{WIFI CONNECTED extra: } nc{[ Transports: WIFI Capabilities: INTERNET&TRUSTED ]}}\nNetworkRequest [ Capabilities: VALIDATED ]";
Check(NetworkStartup.IsReady(healthyNetwork),"validated active Wi-Fi is ready");
Check(!NetworkStartup.IsReady(staleNetwork),"historical network and request validation do not imply internet");
int reconnects=0,probes=0;
Check(await NetworkStartup.EnsureAsync(_=>Task.FromResult(healthyNetwork),_=>{reconnects++;return Task.CompletedTask;},_=>Task.CompletedTask,default)&&reconnects==0,"healthy Wi-Fi is never reset");
Check(await NetworkStartup.EnsureAsync(_=>Task.FromResult(++probes>3?healthyNetwork:staleNetwork),_=>{reconnects++;return Task.CompletedTask;},_=>Task.CompletedTask,default)&&reconnects==1,"stale startup Wi-Fi reconnects once and becomes ready");
reconnects=0;probes=0;
Check(!await NetworkStartup.EnsureAsync(_=>{probes++;return Task.FromResult(staleNetwork);},_=>{reconnects++;return Task.CompletedTask;},_=>Task.CompletedTask,default)&&reconnects==1&&probes==13,"offline startup retry is bounded");
var displayRoot=Path.Combine(Path.GetTempPath(),"LayZDroidDisplay-"+Guid.NewGuid().ToString("N"));
try {
 var displayStore=new Store(displayRoot);var legacy=new Instance{Port=5580,Width=480,Height=854,Package="ata.squid.kaw",RamMb=1536,Cores=2};
 displayStore.Configure(legacy);var disk=Path.Combine(displayStore.InstancePath(legacy),"account-marker");File.WriteAllText(disk,"preserved");displayStore.Save([legacy]);
 var loaded=displayStore.Load().Single();
 Check(loaded.Width==900&&loaded.Height==1600&&loaded.Id==legacy.Id&&loaded.Package==legacy.Package&&loaded.RamMb==1536&&loaded.Cores==2,"legacy display normalizes without changing account identity or resources");
 displayStore.Configure(loaded);Check(File.ReadAllText(disk)=="preserved","display migration preserves existing account storage");
 Check(new Instance().Width==900&&new Instance().Height==1600,"new instance uses standard display");
} finally {if(Directory.Exists(displayRoot))Directory.Delete(displayRoot,true);}
const long Mb=1024*1024;
Check(!Policy.CanStart(4096*Mb,1700*Mb,5000*Mb,1536,0).Allowed,"physical reserve blocks launch");
Check(!Policy.CanStart(8192*Mb,6000*Mb,1000*Mb,1536,0).Allowed,"commit capacity blocks launch");
Check(Policy.CanStart(8192*Mb,6000*Mb,6000*Mb,1536,0).Allowed,"sufficient resources admit launch");
Check(!Policy.CanStart(8192*Mb,6000*Mb,6000*Mb,1536,4500*Mb).Allowed,"in-flight budget is counted");
Check(Policy.NextPort(Enumerable.Range(0,11).Select(n=>5554+n*2).ToHashSet())==5580,"allocation continues beyond ten instances");
Check(Policy.NextPort(new HashSet<int>{5581})==5582,"paired ADB port conflict is respected");
var root=Path.Combine(Path.GetTempPath(),"LayZDroidChecks");
Check(Policy.ExtractionPath(root,"emulator/emulator.exe").StartsWith(Path.GetFullPath(root)),"valid archive path");
Reject(()=>Policy.ExtractionPath(root,"../escape.exe"),"archive traversal rejected");
Reject(()=>Policy.ExtractionPath(root,"/absolute.exe"),"absolute archive path rejected");
Reject(()=>Policy.ExtractionPath(root,"x:stream"),"alternate data stream rejected");
Reject(()=>Policy.ValidateSettings(512,2,480,854),"unsupported RAM rejected");
Reject(()=>Policy.ValidateSettings(1536,0,480,854),"invalid cores rejected");
Policy.ValidateSettings(1536,1,480,854);Check(true,"preview settings accepted");
Policy.ValidateSettings(1536,1,900,1600);Check(true,"900 by 1600 portrait display accepted");
Policy.ValidateSettings(768,1,480,854);Check(true,"experimental 768 MB setting accepted with explicit low-RAM engine option");
Check(!Policy.EffectiveRamMatches(1024,2560),"engine RAM clamp cannot pass");
Check(Policy.EffectiveRamMatches(1536,1536),"matching effective RAM passes");
Check(!Policy.Owns(7,DateTime.UnixEpoch,"a.exe",8,DateTime.UnixEpoch,"a.exe"),"foreign PID rejected");
Check(!Policy.Owns(7,DateTime.UnixEpoch,"a.exe",7,DateTime.UnixEpoch.AddSeconds(1),"a.exe"),"reused PID rejected");
Check(Policy.Owns(7,DateTime.UnixEpoch,"a.exe",7,DateTime.UnixEpoch,"a.exe"),"exact owner accepted");
Console.WriteLine($"PASS all {count} LayZDroid checks");
Check(Policy.SelectAdbPort(5038,new HashSet<int>{5038},_=>false)==5039,"foreign ADB listener gets a separate port");
Check(Policy.SelectAdbPort(5039,new HashSet<int>{5039},p=>p==5039)==5039,"existing owned ADB port is reused");
Check(Policy.SelectAdbPort(5038,new HashSet<int>{5038},_=>throw new IOException("unverifiable listener"))==5039,"unverifiable occupied port is skipped");
var deleteRoot=Path.Combine(Path.GetTempPath(),"LayZDroidDelete-"+Guid.NewGuid().ToString("N"));
try{
 var deleteStore=new Store(deleteRoot);var first=new Instance{Port=5580};var second=new Instance{Port=5582};var items=new List<Instance>{first,second};
 deleteStore.Configure(first);deleteStore.Configure(second);deleteStore.Save(items);deleteStore.SetAdbPort(5039);
 Check(File.ReadAllText(Path.Combine(deleteStore.InstancePath(first),"config.ini")).Contains("hw.lcd.density=240"),"fresh instances default to 240 DPI");
 Check(new Store(deleteRoot).AdbPort==5039,"ADB port survives launcher restart");
 first.Pid=123;Reject(()=>deleteStore.DeleteInstance(items,first),"running instance deletion refused");first.Pid=0;
 deleteStore.DeleteInstance(items,first);
 Check(!Directory.Exists(deleteStore.InstancePath(first))&&!File.Exists(Path.Combine(deleteStore.Avds,first.AvdName+".ini")),"delete removes selected instance files");
 Check(Directory.Exists(deleteStore.InstancePath(second))&&deleteStore.Load().Single().Id==second.Id,"delete preserves other instance and saved selection");
}finally{if(Directory.Exists(deleteRoot))Directory.Delete(deleteRoot,true);}
Reject(()=>ApkMetadata.ReadManifest([0,1,2]),"truncated Android manifest rejected");
Reject(()=>ApkMetadata.ReadManifest(new byte[8]),"non-Android binary rejected");
var fixture=ManifestFixture();Check(ApkMetadata.ReadManifest(fixture).Package=="ata.squid.kaw","Android package parsed from manifest");
var fixtureRoot=Path.Combine(Path.GetTempPath(),"LayZDroidChecks-"+Guid.NewGuid().ToString("N"));
try
{
    var store=new Store(fixtureRoot);var i=new Instance{Port=5580};store.Save([i]);Check(store.Load()[0].Id==i.Id,"instance identity survives restart");store.Configure(i);Check(File.ReadAllText(Path.Combine(store.InstancePath(i),"config.ini")).Contains("hw.cpu.ncore=1"),"fresh AVD uses conservative core setting");
    store.Save([i,i]);Reject(()=>store.Load(),"duplicate instance identity rejected");
}finally{Directory.Delete(fixtureRoot,true);}
Console.WriteLine($"PASS all {count} LayZDroid checks");
Check(Policy.LaunchArguments(new Instance{Port=5580}).Contains("-lowram"),"low-RAM flag prevents implicit image minimum");
Check(Policy.TrustedAdbListener(10,new[]{(10,"own/adb.exe"),(20,"other/HD-Adb.exe")},"own/adb.exe"),"unrelated ADB server does not block trusted listener");
Check(!Policy.TrustedAdbListener(20,new[]{(10,"own/adb.exe"),(20,"other/HD-Adb.exe")},"own/adb.exe"),"foreign listener is refused");
var stageRoot=Path.Combine(Path.GetTempPath(),"LayZDroidStage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stageRoot);Directory.CreateDirectory(Path.Combine(stageRoot,"unrelated"));
try{try{await RuntimeSetup.StageInstallationAsync(stageRoot,Path.Combine(stageRoot,"sdk"),async(path,ct)=>{await File.WriteAllTextAsync(Path.Combine(path,"partial"),"partial",ct);throw new IOException("download failed");},default);throw new Exception("Expected failed setup");}catch(IOException){}Check(Directory.GetDirectories(stageRoot,"stage-*").Length==0,"failed installation cleans its staging directory");Check(Directory.Exists(Path.Combine(stageRoot,"unrelated")),"cleanup preserves unrelated directories");}finally{Directory.Delete(stageRoot,true);}
static byte[] ManifestFixture()
{
    var strings=new[]{"package","ata.squid.kaw","manifest"};using var data=new MemoryStream();using var dw=new BinaryWriter(data);var offsets=new List<int>();foreach(var s in strings){offsets.Add((int)data.Position);var bytes=System.Text.Encoding.UTF8.GetBytes(s);dw.Write((byte)s.Length);dw.Write((byte)bytes.Length);dw.Write(bytes);dw.Write((byte)0);}
    using var file=new MemoryStream();using var w=new BinaryWriter(file);int poolSize=40+(int)data.Length;w.Write((ushort)3);w.Write((ushort)8);w.Write(8+poolSize+56);w.Write((ushort)1);w.Write((ushort)28);w.Write(poolSize);w.Write(3);w.Write(0);w.Write(256);w.Write(40);w.Write(0);foreach(var offset in offsets)w.Write(offset);w.Write(data.ToArray());
    w.Write((ushort)0x102);w.Write((ushort)16);w.Write(56);w.Write(1);w.Write(-1);w.Write(-1);w.Write(2);w.Write((ushort)20);w.Write((ushort)20);w.Write((ushort)1);w.Write((ushort)0);w.Write((ushort)0);w.Write((ushort)0);w.Write(-1);w.Write(0);w.Write(1);w.Write((ushort)8);w.Write((byte)0);w.Write((byte)3);w.Write(1);return file.ToArray();
}
Console.WriteLine($"PASS all {count} LayZDroid checks");
