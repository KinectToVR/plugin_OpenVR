//////////////////////////////////////////////////////////////////////////////
// dprintf.h
// common logging functionality.  
// In debug builds, logs to a file as well as to vr::VRDriverLog and outputdebugstring
// In release builds, logs only to vr::VRDriverLog
//

#pragma once
#include <codecvt>
#include <stdarg.h>
#include <stdio.h>
#include <string>

#include <openvr_driver.h>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include "Windows.h"
#endif

inline void debug_out(const std::string& text)
{
#ifdef _WIN32
    OutputDebugStringA(text.c_str());
#else
    std::fwrite(text.c_str(), 1, text.size(), stderr);
    std::fputc('\n', stderr);
    std::fflush(stderr);
#endif
}

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
        debug_out(buffer);
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

    debug_out(buffer);
}

// Wide String to UTF8 String
inline std::string WStringToString(const std::wstring& w_str)
{
    // ReSharper disable CppClangTidyClangDiagnosticDeprecatedDeclarations
    std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> conv;
    return conv.to_bytes(w_str);
}
