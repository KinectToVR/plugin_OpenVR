using System;
using System.IO;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

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
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages",
            new PackageManager().FindPackagesForUser("")
                .First(x => x.Id.Name is "K2VRTeam.Amethyst.App").Id.FamilyName, "LocalState");
    }

    public static string GetAmethystTempPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages",
            new PackageManager().FindPackagesForUser("")
                .First(x => x.Id.Name is "K2VRTeam.Amethyst.App").Id.FamilyName, "TempState");
    }
}

public static class StorageExtensions
{
    public static void CopyToFolder(this DirectoryInfo source, string destination, bool log = false)
    {
        // Now Create all of the directories
        foreach (var dirPath in source.GetDirectories("*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dirPath.FullName.Replace(source.FullName, destination));

        // Copy all the files & Replaces any files with the same name
        foreach (var newPath in source.GetFiles("*.*", SearchOption.AllDirectories))
            newPath.CopyTo(newPath.FullName.Replace(source.FullName, destination), true);
    }
}