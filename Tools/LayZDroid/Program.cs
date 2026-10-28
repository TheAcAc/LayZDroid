namespace LayZDroid;
static class Program
{
    [STAThread]static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Length>0 && args[0]=="--start-instance")
        {
            try
            {
                using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(4));
                Task.Run(async()=>
                {
                    await RecoveryCommand.ExecuteAsync(args,deadline.Token);
                    // Runner owns redirected emulator log pipes. Keep draining them
                    // until this instance exits, just as the normal launcher does.
                    var store=new Store(Path.GetFullPath(args[3]));
                    var instance=store.Load().Single(i=>i.Id==Guid.Parse(args[1]));
                    try
                    {
                        using var running=System.Diagnostics.Process.GetProcessById(instance.Pid);
                        if(Policy.Owns(instance.Pid,instance.Started,instance.ProcessPath,running.Id,
                            running.StartTime.ToUniversalTime(),running.MainModule?.FileName??""))
                            await running.WaitForExitAsync();
                    }
                    catch(ArgumentException){/* Instance already exited after acknowledgement. */}
                }).GetAwaiter().GetResult();
            }
            catch(Exception ex){Console.Error.WriteLine(ex.ToString());Environment.ExitCode=1;}
            return;
        }
        if(args.Length>0 && args[0]=="--check-storage")
        {
            try{var root=DataFolders.Current.Resolve(args.Skip(1).ToArray());var items=new Store(root).Load();Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{DataFolder=root,Instances=items.Count}));}
            catch(Exception ex){Console.Error.WriteLine(ex.Message);Environment.ExitCode=1;}return;
        }
        using var singleton=new Mutex(true,"Local\\LayZDroidLauncher",out var first);if(!first){MessageBox.Show("LayZDroid is already open.");return;}
        try{Application.Run(new MainForm(new Store(DataFolders.Current.Resolve(args))));}
        catch(Exception ex){MessageBox.Show(ex.Message,"LayZDroid",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
}
