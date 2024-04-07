using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace plugin_OpenVR.Utils;

public class DriverHelper
{
    [DllImport("driver_Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int InstallProxyStub();


    [DllImport("driver_Amethyst.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int UninstallProxyStub();

    [DllImport("oleaut32.dll", PreserveSig = false)]
    public static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk
    );
}