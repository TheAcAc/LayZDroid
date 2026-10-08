using System.Text.Json;
namespace LayZDroid;
public sealed class Instance
{
    public Guid Id{get;set;}=Guid.NewGuid(); public string Name{get;set;}="LayZDroid"; public int Port{get;set;}
    public int RamMb{get;set;}=1024; public int Cores{get;set;}=1; public int Width{get;set;}=480; public int Height{get;set;}=854;
    public string Gpu{get;set;}="host"; public string Package{get;set;}=""; public string Status{get;set;}="Stopped";
    public int Pid{get;set;} public DateTime Started{get;set;} public string ProcessPath{get;set;}="";
    public string AvdName=>"LayZDroid_"+Id.ToString("N");
}
public sealed class Store
{
    public string Root{get;} public string Sdk=>Path.Combine(Root,"runtime","sdk"); public string Avds=>Path.Combine(Root,"instances");
    public string Config=>Path.Combine(Root,"instances.json");
    public Store(string? root=null){Root=Path.GetFullPath(root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LayZDroid"));Directory.CreateDirectory(Root);}
    public List<Instance> Load()
    {
        if(!File.Exists(Config))return [];
        var items=JsonSerializer.Deserialize<List<Instance>>(File.ReadAllText(Config))??throw new InvalidDataException("Instance settings are empty.");
        if(items.Select(i=>i.Id).Distinct().Count()!=items.Count||items.Select(i=>i.Port).Distinct().Count()!=items.Count)throw new InvalidDataException("Duplicate instance identity or port.");
        foreach(var i in items){Policy.ValidateSettings(i.RamMb,i.Cores,i.Width,i.Height);if(i.Port<5554||i.Port>5584||i.Port%2!=0||i.Gpu is not("host" or "software"))throw new InvalidDataException("Invalid saved instance.");}
        return items;
    }
    public void Save(List<Instance> items){var tmp=Config+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(items,new JsonSerializerOptions{WriteIndented=true}));File.Move(tmp,Config,true);}
    public string InstancePath(Instance i)=>Path.Combine(Avds,i.AvdName+".avd");
    public void Configure(Instance i)
    {
        Policy.ValidateSettings(i.RamMb,i.Cores,i.Width,i.Height);
        var dir=InstancePath(i);Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(Avds,i.AvdName+".ini"),$"avd.ini.encoding=UTF-8\npath={dir}\npath.rel=avd/{i.AvdName}.avd\ntarget=android-34\n");
        File.WriteAllText(Path.Combine(dir,"config.ini"),$"avd.id={i.AvdName}\navd.name=LayZDroid\navd.ini.encoding=UTF-8\nabi.type=x86_64\nhw.cpu.arch=x86_64\nimage.sysdir.1=system-images/android-34/default/x86_64/\ntag.id=default\ntag.display=Default Android System Image\ntarget=android-34\nPlayStore.enabled=no\nhw.ramSize={i.RamMb}\nhw.cpu.ncore={i.Cores}\nhw.lcd.width={i.Width}\nhw.lcd.height={i.Height}\nhw.lcd.density=160\nhw.lcd.vsync=30\nhw.gpu.enabled=yes\nhw.gpu.mode={i.Gpu}\nhw.camera.back=none\nhw.camera.front=none\nhw.audioInput=no\nhw.audioOutput=no\nhw.gps=no\nhw.gsmModem=no\nhw.accelerometer=no\nhw.gyroscope=no\nhw.sensors.orientation=no\nhw.sensors.proximity=no\nhw.sensors.light=no\nhw.sensors.pressure=no\nhw.sensors.humidity=no\nhw.sensors.magnetic_field=no\nhw.sensors.temperature=no\nhw.keyboard=yes\nhw.mainKeys=no\nhw.sdCard=no\nshowDeviceFrame=no\ndisk.dataPartition.size=4G\nvm.heapSize=128\nfastboot.forceColdBoot=yes\nfastboot.forceFastBoot=no\n");
    }
}
