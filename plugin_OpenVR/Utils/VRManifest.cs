using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace plugin_OpenVR.Utils;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
internal class VRManifest
{
    // Prevent Warning CS0649: Field '...' is never assigned to, and will always have its default value null:
#pragma warning disable 0649
    public string source { get; set; }
    public List<VRApplication> applications { get; set; }
#pragma warning restore 0649

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    internal class VRApplication
    {
        public string app_key { get; set; }
        public string launch_type { get; set; }
        public string binary_path_windows { get; set; }
        public bool is_dashboard_overlay { get; set; }
        public Dictionary<string, VRStrings> strings { get; set; }

        internal class VRStrings
        {
            public string name { get; set; }
            public string description { get; set; }
        }
    }
}