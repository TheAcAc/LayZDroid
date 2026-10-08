using LayZDroid;
using System.Text.Json;
static class Checks
{
    [STAThread]static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Length==5&&args[0]=="--smoke-game"){
            var live=new Store(args[1]);var items=live.Load();var instance=items.First();var run=new Runner(live);
            Task.Run(async()=>{try{if(await run.Owner(instance,default) is null)await run.Start(instance,new Progress<string>(Console.WriteLine),default);await run.Import(instance,args.Skip(2).ToArray(),default);await run.OpenGame(instance,default);Console.WriteLine("PASS real instance boot, APK installation and game launch command");}finally{live.Save(items);}}).GetAwaiter().GetResult();return;
        }
        if(args.Length==2&&args[0]=="--setup"){RuntimeSetup.InstallAsync(new Store(args[1]),new Progress<string>(Console.WriteLine),default).GetAwaiter().GetResult();Console.WriteLine("PASS complete runtime download, checksums, extraction and activation");return;}
        string root=Path.Combine(Path.GetTempPath(),"LayZDroidUI-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new Store(root);store.Save([new Instance{Name="Weak PC · one instance",Port=5580},new Instance{Name="Workstation · second instance",Port=5582,RamMb=2048,Cores=2,Width=720,Height=1280}]);
            using var form=new MainForm(store);var contents=form.Controls[0];form.Controls.Remove(contents);using var panel=new Panel{BackColor=form.BackColor,ForeColor=form.ForeColor};panel.Controls.Add(contents);panel.CreateControl();
            foreach(var width in new[]{1080,840}){panel.Size=new Size(width,680);panel.PerformLayout();void Create(Control c){c.CreateControl();c.PerformLayout();foreach(Control child in c.Controls)Create(child);}Create(panel);using var bitmap=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));if(args.Length>0){Directory.CreateDirectory(args[0]);bitmap.Save(Path.Combine(args[0],"layzdroid-"+width+".png"));}}
            Console.WriteLine("PASS unshown LayZDroid form renders at normal and minimum width without discovery or emulator input");
            var memory=HostMemory.Read();Console.WriteLine($"Host memory: total {memory.Total/1048576} MB, available {memory.Available/1048576} MB, commit remaining {memory.CommitRemaining/1048576} MB");
            var helperInfo=new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell/v1.0/powershell.exe")){UseShellExecute=false,CreateNoWindow=true};foreach(var arg in new[]{"-NoProfile","-NonInteractive","-Command","Start-Sleep -Seconds 60"})helperInfo.ArgumentList.Add(arg);
            using var helper=System.Diagnostics.Process.Start(helperInfo)??throw new Exception("Cannot start owned cleanup fixture");
            var inventory=Runner.Inventory("ProcessId="+helper.Id);if(inventory.Count!=1||inventory[0].Pid!=helper.Id||string.IsNullOrWhiteSpace(inventory[0].Path))throw new Exception("Cannot read owned helper process identity");Console.WriteLine("PASS process inventory reads populated Windows process properties");
            try{var run=new Runner(store);var identity=(helper.Id,helper.StartTime.ToUniversalTime(),helperInfo.FileName);Task.Run(()=>run.CleanupLaunchedAsync(new Instance{Port=5580},identity,default)).GetAwaiter().GetResult();if(!helper.WaitForExit(5000))throw new Exception("Launch cleanup left owned helper running");Console.WriteLine("PASS failed-launch cleanup stops only its verified owned helper");}finally{if(!helper.HasExited)helper.Kill(false);}
            if(args.Length>1&&args[1]=="--inspect-apk")foreach(var apk in args.Skip(2)){var info=ApkMetadata.Read(apk);Console.WriteLine(JsonSerializer.Serialize(new{File=Path.GetFileName(apk),info.Package,info.Version,info.MinimumApi,info.Abis}));}
        }finally{Directory.Delete(root,true);}
    }
}
