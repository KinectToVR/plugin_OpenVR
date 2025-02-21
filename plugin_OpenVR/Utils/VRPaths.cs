using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace plugin_OpenVR.Utils;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
internal class OpenVrPaths
{
    public static readonly string Path =
        Environment.ExpandEnvironmentVariables(System.IO.Path.Combine(
            "%LocalAppData%", "openvr", "openvrpaths.vrpath"));

    public static OpenVrPaths Read()
    {
        var temp = JsonFile.Read<OpenVrPaths>(Path);
        temp.external_drivers ??= [];

        return temp;
    }

    public static OpenVrPaths TryRead()
    {
        try
        {
            var temp = JsonFile.Read<OpenVrPaths>(Path);
            temp.external_drivers ??= [];

            return temp;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Write()
    {
        JsonFile.Write(Path, this);
    }

    // Prevent Warning CS0649: Field '...' is never assigned to, and will always have its default value null:
#pragma warning disable 0649
    public List<string> config;
    public List<string> external_drivers = [];
    public string jsonid;
    public List<string> log;
    public List<string> runtime;
    public int version;
#pragma warning restore 0649
}

public static class PathUtils
{
    public static string GetShortName(string sLongFileName)
    {
        var buffer = new StringBuilder(259);
        if (GetShortPathName(sLongFileName, buffer, buffer.Capacity) == 0)
            throw new System.ComponentModel.Win32Exception();

        return buffer.ToString();
    }

    [DllImport("kernel32", EntryPoint = "GetShortPathName", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetShortPathName(string longPath, StringBuilder shortPath, int bufSize);

    public static string ShortPath(this string path)
    {
        try
        {
            var result = GetShortName(path);
            return string.IsNullOrEmpty(result) ? path : result;
        }
        catch (Exception)
        {
            return path;
        }
    }
}