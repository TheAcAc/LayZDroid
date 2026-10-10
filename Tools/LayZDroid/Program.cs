namespace LayZDroid;
static class Program
{
    [STAThread]static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
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
