﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;

namespace plugin_OpenVR.Utils;

public static class PackageUtils
{
    public static bool IsAmethystPackaged
    {
        get
        {
            try
            {
                return Package.Current is not null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public static string GetAmethystAppDataPath()
    {
        return ApplicationData.Current.LocalFolder.Path;
    }

    public static string GetAmethystTempPath()
    {
        return ApplicationData.Current.TemporaryFolder.Path;
    }
}

public static class StorageExtensions
{
    public static void CopyToFolder(this DirectoryInfo source, string destination, bool log = false)
    {
        // Now Create all directories
        foreach (var dirPath in source.GetDirectories("*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dirPath.FullName.Replace(source.FullName, destination));

        // Copy all the files & Replaces any files with the same name
        foreach (var newPath in source.GetFiles("*.*", SearchOption.AllDirectories))
            newPath.CopyTo(newPath.FullName.Replace(source.FullName, destination), true);
    }
}

public class InfoBarData
{
    public string Title { get; set; }
    public string Content { get; set; }
    public string Button { get; set; }
    public Action Click { get; set; }
    public bool Closable { get; set; }
    public bool IsOpen { get; set; }

    public (string Title, string Content, string Button, Action Click, bool Closable)? AsPackedData =>
        IsOpen ? (Title, Content, Button, Click, Closable) : null;

    public void ClickAction(object sender, object args)
    {
        Click();
    }
}