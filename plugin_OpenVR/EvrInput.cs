using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Amethyst.Plugins.Contract;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using plugin_OpenVR.Utils;
using Valve.VR;

namespace plugin_OpenVR;

public class ActionsManifest(bool wasNull = false)
{
    [JsonIgnore] public bool WasNull { get; set; } = wasNull;

    [JsonIgnore]
    public bool IsValid
    {
        get
        {
            var defaults = new ActionsManifest();
            return defaults.Defaults.All(Defaults.Contains) &&
                   defaults.Actions.All(Actions.Contains) &&
                   defaults.Sets.All(Sets.Contains) &&
                   defaults.Localization.All(Localization.Contains);
        }
    }

    [JsonProperty("default_bindings")]
    public List<DefaultBindings> Defaults { get; set; } =
    [
        new("oculus_touch", "input_profiles/k2vr.amethyst_oculus_touch.json"),
        new("knuckles", "input_profiles/k2vr.amethyst_knuckles.json"),
        new("vive_controller", "input_profiles/k2vr.amethyst_vive_controller.json"),
        new("hpmotioncontroller", "input_profiles/k2vr.amethyst_hpmotioncontroller.json"),
        new("holographic_controller", "input_profiles/k2vr.amethyst_holographic_controller.json")
    ];

    [JsonProperty("actions")]
    public List<Action> Actions { get; set; } =
    [
        new("/actions/default/in/TrackerFreeze", "boolean", "optional"),
        new("/actions/default/in/FlipToggle", "boolean", "optional"),
        new("/actions/default/in/LeftJoystick", "vector2"),
        new("/actions/default/in/RightJoystick", "vector2"),
        new("/actions/default/in/ConfirmAndSave"),
        new("/actions/default/in/ModeSwap"),
        new("/actions/default/in/FineTune")
    ];

    [JsonProperty("action_sets")]
    public List<Dictionary<string, string>> Sets { get; set; } =
    [
        new()
        {
            { "name", "/actions/default" },
            { "usage", "leftright" }
        }
    ];

    [JsonProperty("localization")]
    public List<Dictionary<string, string>> Localization { get; set; } =
    [
        new()
        {
            { "language_tag", "en_US" },
            { "/actions/default", "Input Actions" },
            { "/actions/default/in/TrackerFreeze", "Freeze Trackers" },
            { "/actions/default/in/FlipToggle", "Toggle Flip" },
            { "/actions/default/in/LeftJoystick", "Left-hand Move/Rotate Controls" },
            { "/actions/default/in/RightJoystick", "Right-hand Move/Rotate Controls" },
            { "/actions/default/in/ConfirmAndSave", "Confirm and Save" },
            { "/actions/default/in/ModeSwap", "Swap Move/Rotate Modes" },
            { "/actions/default/in/FineTune", "Fine-tuning" }
        }
    ];

    public class DefaultBindings(string controllerType = "", string path = "")
    {
        [JsonProperty("controller_type")] public string ControllerType { get; set; } = controllerType;
        [JsonProperty("binding_url")] public string Binding { get; set; } = path;
    }

    public class Action(string name = "", string type = "boolean", string requirement = null, IAmethystHost host = null)
    {
        [JsonProperty("name")] public string Name { get; set; } = name;
        [JsonProperty("type")] public string Type { get; set; } = type;

        [JsonProperty("requirement", NullValueHandling = NullValueHandling.Ignore)]
        public string Requirement { get; set; } = requirement;

        [JsonIgnore] public IAmethystHost Host { get; set; } = host;
        [JsonIgnore] private ulong Handle { get; set; }
        [JsonIgnore] public bool Data => DataDigital.bState;
        [JsonIgnore] public Vector2 State => new(DataAnalog.x, DataAnalog.y);
        [JsonIgnore] private InputDigitalActionData_t DataDigital { get; set; }
        [JsonIgnore] private InputAnalogActionData_t DataAnalog { get; set; }
        [JsonIgnore] public bool Valid => Host is not null && !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Type);

        [JsonIgnore]
        public string Code
        {
            get => Host?.PluginSettings.GetSetting(Handle, string.Empty);
            set => Host?.PluginSettings.SetSetting(Handle, value);
        }

        public async Task<string> Invoke(object data)
        {
            // Exit with a custom message to be shown by the crash handler
            Host?.Log($"Trying to evaluate expression \"{Code}\" for data \"{data}\"...");

            try
            {
                return (await CSharpScript.EvaluateAsync($"var data = {data};{Code.Trim()}",
                    ScriptOptions.Default //.WithImports("Amethyst.Classes")
                        .WithReferences(typeof(IAmethystHost).Assembly)
                        .WithReferences(typeof(SteamVR).Assembly)
                        .AddImports("System.Linq"))).ToString();
            }
            catch (Exception ex)
            {
                return $"Evaluation error: '{ex}'";
            }
        }

        public string GetName(ActionsManifest json, string languageCode)
        {
            return json?.Localization
                .FirstOrDefault(x => x.TryGetValue("language_tag", out var language) &&
                                     language.Contains(languageCode), json?.Localization.FirstOrDefault())
                ?.TryGetValue(Name ?? string.Empty, out var name) ?? false
                ? name
                : Name ?? string.Empty;
        }

        public EVRInputError Register()
        {
            var pHandle = Handle;
            var error = OpenVR.Input.GetActionHandle(Name, ref pHandle);

            Handle = pHandle;
            return error;
        }

        public bool UpdateState(IAmethystHost host)
        {
            return Type switch
            {
                "boolean" => GetDigitalState(host),
                "vector2" => GetAnalogState(host),
                _ => false
            };
        }

        private bool GetDigitalState(IAmethystHost host)
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            var pData = DataDigital;
            var error = OpenVR.Input.GetDigitalActionData(
                Handle, ref pData,
                (uint)Marshal.SizeOf<InputAnalogActionData_t>(),
                OpenVR.k_ulInvalidInputValueHandle);

            DataDigital = pData;

            if (error == EVRInputError.None) return DataDigital.bState;
            host.Log($"GetDigitalActionData call error: {error}", LogSeverity.Error);
            return false;
        }

        private bool GetAnalogState(IAmethystHost host)
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            var pData = DataAnalog;
            var error = OpenVR.Input.GetAnalogActionData(
                Handle, ref pData,
                (uint)Marshal.SizeOf<InputAnalogActionData_t>(),
                OpenVR.k_ulInvalidInputValueHandle);

            DataAnalog = pData;

            if (error == EVRInputError.None) return DataDigital.bState;
            host.Log($"GetAnalogActionData call error: {error}", LogSeverity.Error);
            return false;
        }
    }

    public Action this[string path] => Actions.FirstOrDefault(x => x.Name == path);
}

[method: SetsRequiredMembers]
public class SteamEvrInput(IAmethystHost host)
{
    private IAmethystHost Host { get; set; } = host;
    public ActionsManifest RegisteredActions { get; set; } = new();

    public bool TrackerFreezeActionData => RegisteredActions["/actions/default/in/TrackerFreeze"]?.Data ?? false;
    public bool TrackerFlipToggleData => RegisteredActions["/actions/default/in/FlipToggle"]?.Data ?? false;
    public bool ConfirmAndSaveActionData => RegisteredActions["/actions/default/in/ConfirmAndSave"]?.Data ?? false;
    public bool ModeSwapActionData => RegisteredActions["/actions/default/in/ModeSwap"]?.Data ?? false;
    public bool FineTuneActionData => RegisteredActions["/actions/default/in/FineTune"]?.Data ?? false;
    public Vector2 LeftJoystickActionData => RegisteredActions["/actions/default/in/LeftJoystick"]?.State ?? Vector2.Zero;
    public Vector2 RightJoystickActionData => RegisteredActions["/actions/default/in/RightJoystick"]?.State ?? Vector2.Zero;

    private static (uint Left, uint Right) VrControllerIndexes => (
        SteamVR.Initialized
            ? OpenVR.System?.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand) ??
              OpenVR.k_unTrackedDeviceIndexInvalid
            : OpenVR.k_unTrackedDeviceIndexInvalid,
        SteamVR.Initialized
            ? OpenVR.System?.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand) ??
              OpenVR.k_unTrackedDeviceIndexInvalid
            : OpenVR.k_unTrackedDeviceIndexInvalid
    );

    // The action sets
    private VRActiveActionSet_t _mDefaultActionSet;

    // Note: SteamVR must be initialized beforehand.
    // Preferred type is (vr::VRApplication_Scene)
    public bool InitInputActions()
    {
        if (!SteamVR.Initialized || OpenVR.Input is null || Host is null) return false; // Sanity check

        var manifestPath = Path.Join(PackageUtils.GetAmethystAppDataPath(), "Amethyst", "actions.json");
        RegisteredActions = JsonConvert.DeserializeObject<ActionsManifest>(
            File.ReadAllText(manifestPath)) ?? new ActionsManifest(true);

        if (RegisteredActions.WasNull || !RegisteredActions.IsValid) // Re-generate the action manifest if it's not found
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(RegisteredActions, Formatting.Indented));

        if (!File.Exists(manifestPath))
        {
            Host.Log("Action manifest was not found in the program " +
                     $"({GetProgramLocation().Directory}) directory.", LogSeverity.Error);
            return false; // Return failure status
        }

        // Set the action manifest. This should be in the executable directory.
        // Defined by m_actionManifestPath.
        var error = OpenVR.Input.SetActionManifestPath(manifestPath);
        if (error != EVRInputError.None)
        {
            Host.Log($"Action manifest error: {error}", LogSeverity.Error);
            return false;
        }

        /**********************************************/
        // Here, setup every action with its handler
        /**********************************************/

        // Get action handles for all actions
        RegisteredActions.Actions.ForEach(x => x.Register());

        /**********************************************/
        // Here, setup every action set handle
        /**********************************************/

        // Get set handle Default Set
        ulong defaultSetHandler = 0;
        error = OpenVR.Input.GetActionSetHandle("/actions/default", ref defaultSetHandler);
        if (error != EVRInputError.None)
        {
            Host.Log("ActionSet handle error: {error}", LogSeverity.Error);
            return false;
        }

        /**********************************************/
        // Here, setup action-set handler
        /**********************************************/

        // Default Set
        _mDefaultActionSet.ulActionSet = defaultSetHandler;
        _mDefaultActionSet.ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle;
        _mDefaultActionSet.nPriority = 0;

        // Return OK
        Host.Log("EVR Input Actions initialized OK");
        return true;
    }

    public bool UpdateActionStates()
    {
        if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

        /**********************************************/
        // Check if VR controllers are valid
        /**********************************************/

        if (VrControllerIndexes.Left == OpenVR.k_unTrackedDeviceIndexInvalid ||
            VrControllerIndexes.Right == OpenVR.k_unTrackedDeviceIndexInvalid)
            return true; // Say it's all good, refuse to elaborate, leave

        /**********************************************/
        // Here, update main action sets' handles
        /**********************************************/

        // Update Default ActionSet states
        var error = OpenVR.Input.UpdateActionState(
            [_mDefaultActionSet],
            (uint)Marshal.SizeOf<VRActiveActionSet_t>());

        // ReSharper disable once InvertIf
        if (error != EVRInputError.None)
        {
            Host.Log($"ActionSet (Default) state update error: {error}", LogSeverity.Error);
            return false;
        }

        /**********************************************/
        // Here, update the actions and grab data-s
        /**********************************************/

        return RegisteredActions.Actions.All(x => x.UpdateState(Host) || x.Requirement is "optional");
    }

    public static FileInfo GetProgramLocation()
    {
        return new FileInfo(Assembly.GetExecutingAssembly().Location);
    }
}