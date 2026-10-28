using LayZDroid;
var root=Path.Combine(Path.GetTempPath(),"layzdroid-recovery-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
try
{
    var store=new Store(root);var selected=new Instance{Name="W-",Port=5580,RamMb=3072,Cores=2,Gpu="host"};var other=new Instance{Name="Other",Port=5554,RamMb=2816,Cores=2};
    store.Configure(selected);store.Configure(other);store.Save([selected,other]);
    var config=Path.Combine(store.InstancePath(selected),"config.ini");
    File.AppendAllText(config,"# existing account configuration must be preserved\n");
    var savedConfig=File.ReadAllText(config);
    var ini=Path.Combine(store.Avds,selected.AvdName+".ini");
    File.SetAttributes(config,FileAttributes.ReadOnly);File.SetAttributes(ini,FileAttributes.ReadOnly);
    try
    {
        store.PrepareRecoveryConfiguration(selected);
        Check(File.ReadAllText(config)==savedConfig,"Recovery reuses existing read-only configuration without rewriting it");
    }
    finally{File.SetAttributes(config,FileAttributes.Normal);File.SetAttributes(ini,FileAttributes.Normal);}
    var missing=new Instance();
    try{store.PrepareRecoveryConfiguration(missing);throw new Exception("Missing account config was accepted");}catch(IOException){}
    Check(!Directory.Exists(store.InstancePath(missing)),"Recovery never recreates a missing account directory");
    selected.RamMb=2048;
    try{store.PrepareRecoveryConfiguration(selected);throw new Exception("Changed configuration was accepted");}catch(IOException){}
    selected.RamMb=3072;
    Check(File.ReadAllText(config)==savedConfig,"Mismatched saved settings are rejected without rewriting the account");
    var marker=Path.Combine(store.InstancePath(selected),"account-marker");File.WriteAllText(marker,"preserve-account");var starts=0;
    await RecoveryCommand.ExecuteAsync(["--start-instance",selected.Id.ToString(),"--data",root],default,(saved,instance,ct)=>
    {
        Check(instance.Id==selected.Id&&instance.Port==5580&&instance.Gpu=="host"&&instance.RamMb==3072,"Recovery selects exact saved ID, port and current LayZDroid settings");
        Check(saved.Root==root,"Recovery uses explicit existing data folder");starts++;instance.Pid=123;instance.Status="Ready";
        var latest=saved.Load();latest.Single(i=>i.Id==other.Id).Name="Concurrent edit";saved.Save(latest);return Task.CompletedTask;
    });
    Check(starts==1&&File.ReadAllText(marker)=="preserve-account","Existing account folder is preserved and one Start routine is called");
    Check(store.Load().Single(i=>i.Id==other.Id).Name=="Concurrent edit"&&store.Load().Single(i=>i.Id==selected.Id).Pid==123,"Runtime status merge preserves unrelated edits");
    try{await RecoveryCommand.ExecuteAsync(["--start-instance",Guid.NewGuid().ToString(),"--data",root],default,(_,_,_)=>throw new Exception("must not start"));throw new Exception("Unknown ID was accepted");}catch(IOException){}
    Check(store.Load().Count==2,"Unknown instance ID never creates or replaces instances");
    using var cancelled=new CancellationTokenSource();cancelled.Cancel();
    try{await RecoveryCommand.ExecuteAsync(["--start-instance",selected.Id.ToString(),"--data",root],cancelled.Token,(_,_,_)=>throw new Exception("must not start"));throw new Exception("Cancelled start was accepted");}catch(OperationCanceledException){}
    Check(starts==1,"Cancelled recovery cannot start an instance");
}
finally{Directory.Delete(root,true);}
