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
