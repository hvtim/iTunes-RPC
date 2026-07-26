#include "ShellLinkAutoLaunch.h"
#include "ComInit.h"

#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>

#include <filesystem>

namespace platform_windows {

namespace {

std::filesystem::path ShortcutPath() {
    PWSTR path = nullptr;
    std::filesystem::path result;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_Startup, 0, nullptr, &path))) {
        result = std::filesystem::path(path) / L"iTunes-RPC.lnk";
    }
    if (path) {
        CoTaskMemFree(path);
    }
    return result;
}

} // namespace

bool ShellLinkAutoLaunch::IsEnabled() const {
    auto path = ShortcutPath();
    return !path.empty() && std::filesystem::exists(path);
}

void ShellLinkAutoLaunch::SetEnabled(bool enabled) {
    auto path = ShortcutPath();
    if (path.empty()) {
        return;
    }

    if (!enabled) {
        std::error_code ec;
        std::filesystem::remove(path, ec);
        return;
    }

    EnsureComInitialized();

    wchar_t exePath[MAX_PATH];
    GetModuleFileNameW(nullptr, exePath, MAX_PATH);
    std::filesystem::path installDir = std::filesystem::path(exePath).parent_path();

    IShellLinkW* shellLink = nullptr;
    if (FAILED(CoCreateInstance(CLSID_ShellLink, nullptr, CLSCTX_INPROC_SERVER, IID_IShellLinkW,
            reinterpret_cast<void**>(&shellLink)))) {
        return;
    }

    shellLink->SetPath(exePath);
    shellLink->SetWorkingDirectory(installDir.c_str());
    shellLink->SetDescription(L"iTunes now-playing sync for Discord Rich Presence");

    IPersistFile* persistFile = nullptr;
    if (SUCCEEDED(shellLink->QueryInterface(IID_IPersistFile, reinterpret_cast<void**>(&persistFile)))) {
        persistFile->Save(path.c_str(), TRUE);
        persistFile->Release();
    }

    shellLink->Release();
}

} // namespace platform_windows
