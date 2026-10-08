using System.IO.Compression;
using System.Security.Cryptography;
namespace LayZDroid;
public static class RuntimeSetup
{
    public static bool Ready(Store store)=>File.Exists(Path.Combine(store.Sdk,"ready.json"))&&File.Exists(Path.Combine(store.Sdk,"emulator","emulator.exe"))&&File.Exists(Path.Combine(store.Sdk,"platform-tools","adb.exe"))&&File.Exists(Path.Combine(store.Sdk,"system-images","android-34","default","x86_64","system.img"));
    public static async Task InstallAsync(Store store,IProgress<string> progress,CancellationToken ct)
    {
        if(new DriveInfo(Path.GetPathRoot(store.Root)!).AvailableFreeSpace<6L*1024*1024*1024)throw new IOException("Runtime setup needs at least 6 GB free disk space. Use Choose data folder to select a larger drive, then reopen LayZDroid.");
        Directory.CreateDirectory(Path.Combine(store.Root,"runtime"));
        await StageInstallationAsync(Path.Combine(store.Root,"runtime"),store.Sdk,async(staging,token)=>{
        ct=token;
        using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(30)};
        var packages=new[]{
            ("emulator-windows_x64-16428233.zip","488ed747e82de7e9bb5247becd1ac043c7e5e85d",455342868L,""),
            ("platform-tools_r37.0.1-win.zip","e03e78b1d80b396f1c3358e31251cb31740e1110",8044989L,""),
            ("sys-img/android/x86_64-34_r04.zip","5f6a249f9bc3b1b4c459b13ce2eb646c9680bed1",720747116L,"system-images/android-34/default")};
        foreach(var (url,hash,size,dest) in packages)
        {
            ct.ThrowIfCancellationRequested();var zip=Path.Combine(staging,Path.GetFileName(url));
            using(var response=await http.GetAsync("https://dl.google.com/android/repository/"+url,HttpCompletionOption.ResponseHeadersRead,ct))
            {
                response.EnsureSuccessStatusCode();await using var input=await response.Content.ReadAsStreamAsync(ct);await using var output=File.Create(zip);
                var buffer=new byte[131072];long written=0;int read;int previous=-1;
                while((read=await input.ReadAsync(buffer,ct))>0){written+=read;if(written>size)throw new InvalidDataException("Runtime download exceeded its pinned size.");await output.WriteAsync(buffer.AsMemory(0,read),ct);int pct=(int)(written*100/size);if(pct!=previous){previous=pct;progress.Report($"Downloading {Path.GetFileName(url)}: {pct}%");}}
                if(written!=size)throw new InvalidDataException("Runtime download is incomplete.");
            }
            await using(var data=File.OpenRead(zip)){var actual=Convert.ToHexString(await SHA1.HashDataAsync(data,ct));if(!actual.Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Runtime checksum did not match the pinned upstream package.");}
            progress.Report("Extracting "+Path.GetFileName(url));var destination=Path.Combine(staging,dest);Directory.CreateDirectory(destination);
            using(var archive=ZipFile.OpenRead(zip))
            {
                long expanded=0;
                foreach(var entry in archive.Entries)
                {
                    ct.ThrowIfCancellationRequested();expanded=checked(expanded+entry.Length);if(expanded>6L*1024*1024*1024)throw new InvalidDataException("Runtime archive is too large.");
                    if(((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Runtime contains a symbolic link.");
                    var path=Policy.ExtractionPath(destination,entry.FullName);
                    if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(path);continue;}
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path,false);
                }
            }
            File.Delete(zip);
        }
        },ct);
        progress.Report("Runtime ready. Add an instance to begin.");
    }
    public static async Task StageInstallationAsync(string parent,string sdk,Func<string,CancellationToken,Task> populate,CancellationToken ct)
    {
        if(Directory.Exists(sdk))throw new IOException("A runtime directory already exists. Keep it for diagnosis; use a fresh data directory for setup.");
        Directory.CreateDirectory(parent);var staging=Path.Combine(Path.GetFullPath(parent),"stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        try
        {
            await populate(staging,ct);ct.ThrowIfCancellationRequested();
            foreach(var file in new[]{"emulator/emulator.exe","platform-tools/adb.exe","system-images/android-34/default/x86_64/system.img"})if(!File.Exists(Path.Combine(staging,file)))throw new InvalidDataException("Runtime is missing required files.");
            File.WriteAllText(Path.Combine(staging,"ready.json"),"{\"engine\":\"37.2.12\",\"api\":34,\"imageRevision\":4,\"distribution\":\"upstream-download\"}");Directory.Move(staging,sdk);
        }
        finally
        {
            var prefix=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(Directory.Exists(staging)&&Path.GetFullPath(staging).StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(staging).StartsWith("stage-",StringComparison.Ordinal))Directory.Delete(staging,true);
        }
    }
}
