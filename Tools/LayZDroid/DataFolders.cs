namespace LayZDroid;

public sealed class DataFolders(string preferenceFile, string defaultRoot)
{
    public static DataFolders Current => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LayZDroid", "data-folder.txt"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LayZDroid"));
    public string Resolve(string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--data")) throw new InvalidDataException("Use --data followed by an absolute instance folder.");
        bool explicitRoot = args.Length == 2;
        bool saved = File.Exists(preferenceFile);
        string root = explicitRoot ? args[1] : saved ? File.ReadAllText(preferenceFile).Trim() : defaultRoot;
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) throw new InvalidDataException("The saved data folder is invalid. Choose an absolute folder using --data.");
        root = Path.GetFullPath(root);
        if (saved && !explicitRoot && !Directory.Exists(root)) throw new DirectoryNotFoundException("Your saved LayZDroid data folder is unavailable: " + root + ". Reconnect its drive or select the existing folder; no empty replacement was created.");
        // Persist command-line routing as well as picker selections, outside the executable directory.
        if (explicitRoot) { Directory.CreateDirectory(root); Remember(root); }
        return root;
    }
    public static string FromSelection(string selected)
    {
        selected = Path.GetFullPath(selected);
        return File.Exists(Path.Combine(selected, "instances.json")) || Directory.Exists(Path.Combine(selected, "instances")) || Directory.Exists(Path.Combine(selected, "runtime"))
            ? selected : Path.Combine(selected, "LayZDroid");
    }
    public void Remember(string root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(preferenceFile)!);
        File.WriteAllText(preferenceFile + ".tmp", Path.GetFullPath(root));
        File.Move(preferenceFile + ".tmp", preferenceFile, true);
    }
}
