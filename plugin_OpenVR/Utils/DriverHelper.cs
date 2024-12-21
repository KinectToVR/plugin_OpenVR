using System;
using System.Runtime.InteropServices;

namespace plugin_OpenVR.Utils;

public class DriverHelper
{
    public static int InstallDriverProxyStub(bool emulated)
    {
        return emulated ? Install00ProxyStub() : InstallProxyStub();
    }

    public static int UninstallDriverProxyStub(bool emulated)
    {
        return emulated ? Uninstall00ProxyStub() : UninstallProxyStub();
    }

    [DllImport("driver_Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int InstallProxyStub();

    [DllImport("driver_Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int UninstallProxyStub();

    [DllImport("driver_00Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Install00ProxyStub();

    [DllImport("driver_00Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Uninstall00ProxyStub();

    [DllImport("oleaut32.dll", PreserveSig = false)]
    public static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk
    );
}