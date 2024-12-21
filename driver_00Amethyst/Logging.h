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