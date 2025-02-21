using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;

namespace plugin_OpenVR.Utils;

public static class PathsHandler
{
    public static async Task Setup()
    {
        if (IsAmethystPackaged) return;

        var root = await StorageFolder.GetFolderFromPathAsync(
            Path.Join(ProgramLocation.DirectoryName!));

        LocalFolderUnpackaged = await (await root
                .CreateFolderAsync("AppData", CreationCollisionOption.OpenIfExists))
            .CreateFolderAsync("LocalState", CreationCollisionOption.OpenIfExists);
    }

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

    public static FileInfo ProgramLocation => new(Assembly.GetExecutingAssembly().Location);

    public static StorageFolder LocalFolder => IsAmethystPackaged ? ApplicationData.Current.LocalFolder : LocalFolderUnpackaged;

    public static StorageFolder LocalFolderUnpackaged { get; set; } // Assigned on Setup()
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