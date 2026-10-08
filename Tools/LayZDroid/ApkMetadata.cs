using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
namespace LayZDroid;
public sealed record ApkInfo(string Package,string Version,int MinimumApi,string[] Abis);
public static class ApkMetadata
{
    public static ApkInfo Read(string path)
    {
        using var archive=ZipFile.OpenRead(path);var manifest=archive.GetEntry("AndroidManifest.xml")??throw new InvalidDataException("APK has no Android manifest.");if(manifest.Length>4*1024*1024)throw new InvalidDataException("APK manifest is too large.");
        using var stream=manifest.Open();using var data=new MemoryStream();stream.CopyTo(data);var info=ReadManifest(data.ToArray());var abis=archive.Entries.Where(e=>e.FullName.StartsWith("lib/")&&e.FullName.EndsWith(".so")).Select(e=>e.FullName.Split('/')[1]).Distinct().ToArray();return info with{Abis=abis};
    }
    public static ApkInfo ReadManifest(byte[] data)
    {
        void Bounds(int offset,int length){if(offset<0||length<0||offset>data.Length-length)throw new InvalidDataException("Truncated APK manifest.");}
        ushort U16(int o){Bounds(o,2);return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(o,2));}
        int I32(int o){Bounds(o,4);return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(o,4));}
        if(data.Length<8||U16(0)!=3||U16(2)!=8||I32(4)!=data.Length)throw new InvalidDataException("Invalid Android binary manifest.");
        List<string> strings=[];string package="",version="";int min=1;
        for(int pos=8;pos<data.Length;)
        {
            Bounds(pos,8);int type=U16(pos),header=U16(pos+2),size=I32(pos+4);if(size<8||header<8||header>size)throw new InvalidDataException("Invalid manifest chunk.");Bounds(pos,size);
            if(type==1)
            {
                if(header<28)throw new InvalidDataException("Invalid string pool.");int count=I32(pos+8),flags=I32(pos+16),start=I32(pos+20);if(count<0||count>65536||start<header||start>=size)throw new InvalidDataException("Invalid string pool.");strings.Clear();
                for(int n=0;n<count;n++)
                {
                    Bounds(pos+header+n*4,4);int at=checked(pos+start+I32(pos+header+n*4));
                    int Len8(){Bounds(at,1);int first=data[at++];if((first&128)==0)return first;Bounds(at,1);return((first&127)<<8)|data[at++];}
                    int Len16(){int first=U16(at);at+=2;if((first&0x8000)==0)return first;int second=U16(at);at+=2;return((first&0x7fff)<<16)|second;}
                    if((flags&256)!=0){_=Len8();int length=Len8();Bounds(at,length+1);if(at+length+1>pos+size||data[at+length]!=0)throw new InvalidDataException("Invalid UTF-8 string.");strings.Add(Encoding.UTF8.GetString(data,at,length));}
                    else{int length=checked(Len16()*2);Bounds(at,length+2);if(at+length+2>pos+size||U16(at+length)!=0)throw new InvalidDataException("Invalid UTF-16 string.");strings.Add(Encoding.Unicode.GetString(data,at,length));}
                }
            }
            if(type==0x102)
            {
                string S(int index)=>index>=0&&index<strings.Count?strings[index]:throw new InvalidDataException("Invalid manifest string index.");
                if(header<16||size<header+20)throw new InvalidDataException("Invalid XML element.");int ext=pos+header;string element=S(I32(ext+4));int offset=U16(ext+8),attrSize=U16(ext+10),count=U16(ext+12);if(attrSize<20||offset<20||offset+(long)attrSize*count>size-header)throw new InvalidDataException("Invalid manifest attributes.");
                for(int n=0;n<count;n++){int a=ext+offset+n*attrSize;string key=S(I32(a+4));int raw=I32(a+8);string value=raw>=0?S(raw):data[a+15]==3?S(I32(a+16)):I32(a+16).ToString();if(element=="manifest"&&key=="package")package=value;if(element=="manifest"&&key=="versionCode")version=value;if(element=="uses-sdk"&&key=="minSdkVersion"&&int.TryParse(value,out var api))min=api;}
            }
            pos=checked(pos+size);
        }
        if(!Regex.IsMatch(package,@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$"))throw new InvalidDataException("APK package identity is missing or invalid.");return new(package,version,min,[]);
    }
}
