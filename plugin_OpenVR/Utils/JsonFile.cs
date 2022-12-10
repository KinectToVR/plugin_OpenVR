using System.Diagnostics.CodeAnalysis;
using System.IO;
using Newtonsoft.Json;

namespace plugin_OpenVR.Utils;

internal class JsonFile
{
    public static T Read<T>(string path)
    {
        return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
    }

    public static void Write(string path, object obj)
    {
        File.WriteAllText(path, JsonConvert.SerializeObject(obj, Formatting.Indented));
    }
}