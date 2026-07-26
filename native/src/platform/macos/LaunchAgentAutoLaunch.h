#pragma once

#include "core/AutoLaunch.h"

namespace platform_macos {

// Manages a per-user LaunchAgent plist for login autostart via plain file
// I/O - a simple RunAtLoad agent doesn't need the ServiceManagement
// framework's registration APIs.
class LaunchAgentAutoLaunch : public core::AutoLaunch {
public:
    bool IsEnabled() const override;
    void SetEnabled(bool enabled) override;
};

} // namespace platform_macos
