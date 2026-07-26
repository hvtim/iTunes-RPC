#include "LaunchAgentAutoLaunch.h"

#include <mach-o/dyld.h>

#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <vector>

namespace platform_macos {

namespace {

constexpr const char* kLabel = "com.hvtim.itunes-rpc";

std::filesystem::path PlistPath() {
    const char* home = std::getenv("HOME");
    if (!home) {
        return {};
    }
    return std::filesystem::path(home) / "Library" / "LaunchAgents" / (std::string(kLabel) + ".plist");
}

// _NSGetExecutablePath over NSBundle - keeps this file plain C++ (no
// Objective-C runtime needed just to find our own path), same rationale
// as Windows' GetModuleFileNameW call in ShellLinkAutoLaunch.
std::string ExecutablePath() {
    uint32_t size = 0;
    _NSGetExecutablePath(nullptr, &size); // first call only reports the required size
    std::vector<char> buffer(size);
    if (_NSGetExecutablePath(buffer.data(), &size) != 0) {
        return "";
    }
    return std::string(buffer.data());
}

} // namespace

bool LaunchAgentAutoLaunch::IsEnabled() const {
    auto path = PlistPath();
    return !path.empty() && std::filesystem::exists(path);
}

void LaunchAgentAutoLaunch::SetEnabled(bool enabled) {
    auto path = PlistPath();
    if (path.empty()) {
        return;
    }

    if (!enabled) {
        std::error_code ec;
        std::filesystem::remove(path, ec);
        return;
    }

    std::string exe = ExecutablePath();
    if (exe.empty()) {
        return;
    }

    std::filesystem::create_directories(path.parent_path());

    std::ofstream out(path);
    out << "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
        << "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
        << "<plist version=\"1.0\">\n"
        << "<dict>\n"
        << "  <key>Label</key>\n"
        << "  <string>" << kLabel << "</string>\n"
        << "  <key>ProgramArguments</key>\n"
        << "  <array>\n"
        << "    <string>" << exe << "</string>\n"
        << "  </array>\n"
        << "  <key>RunAtLoad</key>\n"
        << "  <true/>\n"
        << "</dict>\n"
        << "</plist>\n";
}

} // namespace platform_macos
