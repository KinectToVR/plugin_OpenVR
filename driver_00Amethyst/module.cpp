#include <combaseapi.h>
#include "winrt.hpp"

_Use_decl_annotations_ STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv) try
{
    return CLASS_E_CLASSNOTAVAILABLE;
}
catch (...)
{
    return winrt::to_hresult();
}
