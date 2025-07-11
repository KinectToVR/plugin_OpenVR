//////////////////////////////////////////////////////////////////////////////
// dprintf.h
// common logging functionality.  
// In debug builds, logs to a file as well as to vr::VRDriverLog and outputdebugstring
// In release builds, logs only to vr::VRDriverLog
//

#pragma once
#include <stdarg.h>
#include <stdio.h>
#include <string>

#include <openvr_driver.h>
#include <windows.h>

inline void logMessage(const std::string& message, ...)
{
    va_list args;
    char buffer[2048];

    auto fmt = message.c_str();

    va_start(args, fmt);
    vsprintf(buffer, fmt, args);
    va_end(args);
    
    if (vr::VRDriverContext() && vr::VRDriverLog())
    {
        vr::VRDriverLog()->Log(buffer);
    }
    else
    {
        OutputDebugStringA(buffer);
    }
}

inline void logMessageVerbose(const std::string& message, ...)
{
    va_list args;
    char buffer[2048];

    auto fmt = message.c_str();

    va_start(args, fmt);
    vsprintf(buffer, fmt, args);
    va_end(args);

    OutputDebugStringA(buffer);
}

// Wide String to UTF8 String
inline std::string WStringToString(const std::wstring& w_str)
{
    const int count = WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), w_str.length(), nullptr, 0, nullptr, nullptr);
    std::string str(count, 0);
    WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), -1, str.data(), count, nullptr, nullptr);
    return str;
}
