//////////////////////////////////////////////////////////////////////////////
// dprintf.h
// common logging functionality.  
// In debug builds, logs to a file as well as to vr::VRDriverLog and outputdebugstring
// In release builds, logs only to vr::VRDriverLog
//

#pragma once
#include <openvr_driver.h>
#include <string>

inline void logMessage(const std::string& message)
{
    if (vr::VRDriverContext() && vr::VRDriverLog())
    {
        vr::VRDriverLog()->Log(message.c_str());
    }
    else
    {
        OutputDebugStringA(message.c_str());
    }
}

// Wide String to UTF8 String
inline std::string WStringToString(const std::wstring& w_str)
{
    const int count = WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), w_str.length(), nullptr, 0, nullptr, nullptr);
    std::string str(count, 0);
    WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), -1, str.data(), count, nullptr, nullptr);
    return str;
}
