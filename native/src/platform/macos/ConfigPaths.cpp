#include "core/ConfigPaths.h"

#include <cstdlib>

namespace core {

namespace {

std::filesystem::path HomeDirectory() {
    const char* home = std::getenv("HOME");
    return home ? std::filesystem::path(home) : std::filesystem::path();
}

} // namespace

std::filesystem::path GetConfigDirectory() {
    auto home = HomeDirectory();
    if (home.empty()) {
        return {};
    }
    return home / "Library" / "Application Support" / "iTunes-RPC";
}

std::filesystem::path GetConfigFilePath() {
    return GetConfigDirectory() / "config.json";
}

std::filesystem::path GetLogFilePath() {
    return GetConfigDirectory() / "itunes-rpc.log";
}

std::filesystem::path GetPidFilePath() {
    return GetConfigDirectory() / "itunesrpcd.pid";
}

std::filesystem::path GetStatusFilePath() {
    return GetConfigDirectory() / "itunesrpcd.status.json";
}

} // namespace core
