#pragma once
#include "../arch.h"
#include <windef.h>

// Enum           : PreferredAppMode, Type: int
// Data           :   constant 0x0, Constant, Type: int, Default
// Data           :   constant 0x1, Constant, Type: int, AllowDark
// Data           :   constant 0x2, Constant, Type: int, ForceDark
// Data           :   constant 0x3, Constant, Type: int, ForceLight
// Data           :   constant 0x4, Constant, Type: int, Max
enum class PreferredAppMode : INT
{
    Default = 0,
    AllowDark = 1,
    ForceDark = 2,
    ForceLight = 3,
    Max = 4
};

using PFN_SET_PREFERRED_APP_MODE = PreferredAppMode(WINAPI*)(PreferredAppMode appMode);
using PFN_ALLOW_DARK_MODE_FOR_WINDOW = BOOL(WINAPI*)(HWND window, bool allow);
using PFN_SHOULD_SYSTEM_USE_DARK_MODE = BOOL(WINAPI*)();
