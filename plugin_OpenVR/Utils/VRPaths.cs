using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace plugin_OpenVR.Utils;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
internal class OpenVrPaths
{
    private static readonly string Path =
        Environment.ExpandEnvironmentVariables(System.IO.Path.Combine(
            "%LocalAppData%", "openvr", "openvrpaths.vrpath"));

    public static OpenVrPaths Read()
    {
        var temp = JsonFile.Read<OpenVrPaths>(Path);
        temp.external_drivers ??= new List<string>();

        return temp;
    }

    public void Write()
    {
        JsonFile.Write(Path, this);
    }

    // Prevent Warning CS0649: Field '...' is never assigned to, and will always have its default value null:
#pragma warning disable 0649
    public List<string> config;
    public List<string> external_drivers = new();
    public string jsonid;
    public List<string> log;
    public List<string> runtime;
    public int version;
#pragma warning restore 0649
}