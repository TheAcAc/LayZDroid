namespace LayZDroid;
static class Program
{
    [STAThread]static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var singleton=new Mutex(true,"Local\\LayZDroidLauncher",out var first);if(!first){MessageBox.Show("LayZDroid is already open.");return;}
        try{var preference=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LayZDroid","data-folder.txt");var root=args.Length==2&&args[0]=="--data"?args[1]:File.Exists(preference)?File.ReadAllText(preference).Trim():null;Application.Run(new MainForm(new Store(root)));}
        catch(Exception ex){MessageBox.Show(ex.Message,"LayZDroid",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
}
