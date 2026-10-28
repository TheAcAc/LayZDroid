using System.Security.Cryptography;
using System.Text;

namespace LayZDroid;

public static class RecoveryCommand
{
    public static async Task ExecuteAsync(string[] args,CancellationToken ct,
        Func<Store,Instance,CancellationToken,Task>? start=null)
    {
        if(args.Length!=4 || args[0]!="--start-instance" || !Guid.TryParse(args[1],out var id) ||
            args[2]!="--data" || !Path.IsPathFullyQualified(args[3]))
            throw new ArgumentException("Use --start-instance <saved instance ID> --data <existing data folder>.");
        var root=Path.GetFullPath(args[3]);
        if(!File.Exists(Path.Combine(root,"instances.json")))throw new IOException("Existing LayZDroid instance registry is missing.");
        var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant()+id)));
        using var gate=new Semaphore(1,1,@"Local\LayZDroidRecovery-"+key);
        if(!gate.WaitOne(0))throw new IOException("This LayZDroid instance already has a start request in progress.");
        try
        {
            ct.ThrowIfCancellationRequested();
            var store=new Store(root);
            var instance=store.Load().SingleOrDefault(i=>i.Id==id)??throw new IOException("Saved LayZDroid instance was not found; no instance was created.");
            if(!Directory.Exists(store.InstancePath(instance)))throw new IOException("Existing instance folder is missing; recovery will not recreate it.");
            await (start??StartSavedInstanceAsync)(store,instance,ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            // Merge runtime status into the newest registry; preserve other instances/settings.
            var latest=store.Load();var saved=latest.Single(i=>i.Id==id);
            saved.Pid=instance.Pid;saved.Started=instance.Started;saved.ProcessPath=instance.ProcessPath;saved.Status=instance.Status;
            store.Save(latest);
            Console.WriteLine("LAYZDROID_START_OK:"+id);
            Console.Out.Flush();
        }
        finally{gate.Release();}
    }

    private static async Task StartSavedInstanceAsync(Store store,Instance instance,CancellationToken ct)
    {
        var runner=new Runner(store);
        var owner=await runner.Owner(instance,ct);
        if(owner is null)
            await runner.Start(instance,new Progress<string>(Console.WriteLine),ct,useExistingConfiguration:true);
        else
        {
            instance.Pid=owner.Value.Pid;instance.Started=owner.Value.Start;instance.ProcessPath=owner.Value.Path;
            instance.Status="Running · check game";
        }
    }
}
