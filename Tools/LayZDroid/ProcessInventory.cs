using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LayZDroid;

public readonly record struct ProcessIdentity(DateTime Start,string Path);

public static class ProcessInventory
{
    public static (int Pid,DateTime Start,string Path,string Args)? Resolve(int pid,string reportedPath,string args,Func<ProcessIdentity?> inspect,string? reportedCreation=null)
    {
        var current=inspect();
        if(current is null)return null;
        if(string.IsNullOrWhiteSpace(current.Value.Path))
            throw new IOException("Cannot verify Windows process "+pid+". Instance startup, stopping and deletion are blocked until its identity can be checked.");
        if(reportedCreation is not null)
        {
            // WMI timestamps carry microseconds and a signed UTC offset in minutes.
            if(reportedCreation.Length!=25||reportedCreation[14]!='.'||reportedCreation[21] is not ('+' or '-')||
                !DateTime.TryParseExact(reportedCreation[..14],"yyyyMMddHHmmss",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var local)||
                !int.TryParse(reportedCreation.AsSpan(15,6),out int micros)||micros is <0 or >999999||
                !int.TryParse(reportedCreation.AsSpan(22,3),out int minutes)||minutes is <0 or >840)
                throw new IOException("Cannot verify creation time for Windows process "+pid+".");
            var reported=new DateTimeOffset(local.AddTicks(micros*10L),TimeSpan.FromMinutes(reportedCreation[21]=='-'?-minutes:minutes)).UtcDateTime;
            if(current.Value.Start.Ticks/10!=reported.Ticks/10)return null;
        }
        // Reject stale WMI metadata if that PID now belongs to another executable.
        if(!string.IsNullOrWhiteSpace(reportedPath)&&!string.Equals(reportedPath,current.Value.Path,StringComparison.OrdinalIgnoreCase))return null;
        return (pid,current.Value.Start,current.Value.Path,args);
    }

    public static ProcessIdentity? Inspect(int pid)
    {
        // Limited query rights suffice; do not request permission to inspect VM memory.
        using var handle=OpenProcess(0x1000|0x100000,false,pid);
        if(handle.IsInvalid)
        {
            int error=Marshal.GetLastWin32Error();
            if(error==87)return null; // Process disappeared between enumeration and inspection.
            throw Failure(pid,error);
        }
        bool Exited()
        {
            uint result=WaitForSingleObject(handle,0);
            if(result==0)return true;
            if(result==258)return false;
            throw Failure(pid,Marshal.GetLastWin32Error());
        }
        if(Exited())return null;
        if(!GetProcessTimes(handle,out long created,out _,out _,out _))
        {
            int error=Marshal.GetLastWin32Error();if(Exited())return null;throw Failure(pid,error);
        }
        var path=new StringBuilder(32768);int length=path.Capacity;
        if(!QueryFullProcessImageName(handle,0,path,ref length))
        {
            int error=Marshal.GetLastWin32Error();if(Exited())return null;throw Failure(pid,error);
        }
        return Exited()?null:new ProcessIdentity(DateTime.FromFileTimeUtc(created),path.ToString());
    }

    static IOException Failure(int pid,int error)=>new("Cannot verify Windows process "+pid+". Instance startup, stopping and deletion are blocked until its identity can be checked.",new Win32Exception(error));
    [DllImport("kernel32.dll",SetLastError=true)]static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(SafeProcessHandle process,uint milliseconds);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(SafeProcessHandle process,out long creation,out long exit,out long kernel,out long user);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(SafeProcessHandle process,uint flags,StringBuilder path,ref int length);
}
