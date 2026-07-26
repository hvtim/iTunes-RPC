#pragma once

#include "core/AutoLaunch.h"

namespace platform_windows {

// Manages the Startup-folder shortcut via native IShellLinkW COM - no
// Windows Script Host dependency, unlike the old WScript.Shell approach.
class ShellLinkAutoLaunch : public core::AutoLaunch {
public:
    bool IsEnabled() const override;
    void SetEnabled(bool enabled) override;
};

} // namespace platform_windows
