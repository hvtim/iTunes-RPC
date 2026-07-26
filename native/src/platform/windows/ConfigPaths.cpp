#include "core/ConfigPaths.h"

#include <windows.h>
#include <shlobj.h>

namespace core {

std::filesystem::path GetConfigDirectory() {
    PWSTR path = nullptr;
    std::filesystem::path result;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &path))) {
        result = std::filesystem::path(path) / L"iTunes-RPC";
    }
    if (path) {
        CoTaskMemFree(path);
    }
    return result;
}

std::filesystem::path GetConfigFilePath() {
    return GetConfigDirectory() / L"config.json";
}

std::filesystem::path GetLogFilePath() {
    return GetConfigDirectory() / L"itunes-rpc.log";
}

} // namespace core
