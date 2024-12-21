using System;
using System.Runtime.InteropServices;

namespace plugin_OpenVR.Utils;

public class DriverHelper
{
    public static int InstallDriverProxyStub(bool emulated)
    {
        return emulated ? Methods00.InstallProxyStub() : Methods.InstallProxyStub();
    }

    public static int UninstallDriverProxyStub(bool emulated)
    {
        return emulated ? Methods00.UninstallProxyStub() : Methods.UninstallProxyStub();
    }

    [DllImport("oleaut32.dll", PreserveSig = false)]
    public static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk
    );

    internal static class Methods
    {
        [DllImport("driver_Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int InstallProxyStub();

        [DllImport("driver_Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int UninstallProxyStub();
    }

    internal static class Methods00
    {
        [DllImport("driver_00Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int InstallProxyStub();

        [DllImport("driver_00Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int UninstallProxyStub();
    }
}