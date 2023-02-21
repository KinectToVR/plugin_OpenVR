<h1 dir=auto>
<b>OpenVR</b>
<a style="color:#9966cc;" href="https://github.com/KinectToVR/Amethyst">Amethyst</a>
<text>service plugin</text>
</h1>

## **License**
This project is licensed under the GNU GPL v3 License 

## **Overview**
This repo is a mixed implementation of the `IServiceEndpoint` interface,  
providing Amethyst support for SteamVR (OpenVR), using the OpenVR SDK.  
[The bound API](https://github.com/ValveSoftware/openvr) provided by Valve, and [the plugin itself](https://github.com/KinectToVR/plugin_OpenVR/tree/main/plugin_OpenVR) are written in C#  
This repository also contains the [Amethyst driver for OpenVR](https://github.com/KinectToVR/plugin_OpenVR/tree/main/driver_Amethyst), using gRPC.

## **Downloads**
You're going to find built plugins in [repo Releases](https://github.com/KinectToVR/plugin_OpenVR/releases/latest).

## **Build & Deploy**
Both build and deployment instructions [are available here](https://github.com/KinectToVR/plugin_OpenVR/blob/main/.github/workflows/build.yml).
 - `vcpkg install glog:x64-windows-static gflags:x64-windows-static protobuf:x64-windows-static grpc:x64-windows-static`
 - Open in Visual Studio and build the OpenVR driver (`driver_Amethyst` → `Build`)  
   You'll need to register it by `vrpathreg adddriver <path to driver_Amethyst>`
 - Publish the Amethyst plugin using the prepared publish profile  
   (`plugin_KinectV1` → `Publish` → `Publish` → `Open folder`)
 - Copy the published plugin to the `plugins` folder of your local Amethyst installation  
   or register by adding it to `$env:AppData\Amethyst\amethystpaths.k2path`
   ```jsonc
   {
    "external_plugins": [
        // Add the published plugin path here, this is an example:
        "F:\\source\\repos\\plugin_OpenVR\\plugin_OpenVR\\bin\\Release\\Publish"
    ]
   }
   ```

## **Wanna make one too? (K2API Devices Docs)**
[This repository](https://github.com/KinectToVR/Amethyst.Plugins.Templates) contains templates for plugin types supported by Amethyst.<br>
Install the templates by `dotnet new install Amethyst.Plugins.Templates::1.2.0`  
and use them in Visual Studio (recommended) or straight from the DotNet CLI.  
The project templates already contain most of the needed documentation,  
although please feel free to check out [the official wesite](https://docs.k2vr.tech/) for more docs sometime.

The build and publishment workflow is the same as in this repo (excluding vendor deps).  