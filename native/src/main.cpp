#include "core/AppConfig.h"
#include "core/ConfigPaths.h"
#include "core/Log.h"
#include "core/PresenceEngine.h"

#include "platform/windows/ITunesMediaSource.h"
#include "platform/windows/PipeIpcTransport.h"
#include "platform/windows/ShellLinkAutoLaunch.h"
#include "platform/windows/SmtcMediaSource.h"
#include "platform/windows/StringConvert.h"
#include "platform/windows/TextPrompt.h"
#include "platform/windows/TrayIcon.h"
#include "platform/windows/WinHttpAlbumArtLookup.h"

#include <windows.h>

#include <cstdio>
#include <memory>
#include <string>

namespace {

std::unique_ptr<core::MediaSource> MakeMediaSource(const std::string& id) {
    if (id == "iTunes") {
        return std::make_unique<platform_windows::ITunesMediaSource>();
    }
    return std::make_unique<platform_windows::SmtcMediaSource>(id);
}

// Useful for live debugging while running from a terminal - Log::Write
// also always writes to the log file, which is the only way to see
// anything once the app is running windowless via autorun.
void AttachDebugConsole() {
    AllocConsole();
    FILE* dummy = nullptr;
    freopen_s(&dummy, "CONOUT$", "w", stdout);
    freopen_s(&dummy, "CONOUT$", "w", stderr);
    SetConsoleTitleW(L"iTunes-RPC - debug console");
}

} // namespace

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, PWSTR, int) {
    AttachDebugConsole();

    core::Log::Init(core::GetLogFilePath());
    core::Log::Write("iTunes-RPC starting...");

    core::AppConfig config = core::LoadConfig(core::GetConfigFilePath());
    std::string currentMediaSourceId = config.mediaSource;

    platform_windows::ShellLinkAutoLaunch autoLaunch;

    core::PresenceEngine engine(
        config,
        MakeMediaSource(currentMediaSourceId),
        std::make_unique<platform_windows::WinHttpAlbumArtLookup>(),
        [] { return std::make_unique<platform_windows::PipeIpcTransport>(); });

    nativeui::TrayIcon tray;
    if (!tray.Create(hInstance, L"iTunes-RPC")) {
        MessageBoxW(nullptr, L"Failed to create the tray icon.", L"iTunes-RPC", MB_ICONERROR);
        return 1;
    }
    tray.SetInitialState(config, autoLaunch.IsEnabled());

    tray.OnConfigChanged = [&](const core::AppConfig& newConfig) {
        core::SaveConfig(newConfig, core::GetConfigFilePath());

        std::unique_ptr<core::MediaSource> newMediaSource;
        if (newConfig.mediaSource != currentMediaSourceId) {
            currentMediaSourceId = newConfig.mediaSource;
            newMediaSource = MakeMediaSource(currentMediaSourceId);
        }

        engine.UpdateConfig(newConfig, std::move(newMediaSource));
    };

    tray.OnStartAtLoginChanged = [&](bool enabled) {
        autoLaunch.SetEnabled(enabled);
    };

    tray.OnEditApplicationId = [&](std::wstring& value) {
        nativeui::PromptForText(tray.Hwnd(), L"iTunes-RPC", L"Discord Application ID:", value);
    };

    tray.OnEditCustomArtUrl = [&](std::wstring& value) {
        nativeui::PromptForText(tray.Hwnd(), L"iTunes-RPC", L"Image URL (512x512 recommended):", value);
    };

    tray.OnRefreshMediaSources = [] { return platform_windows::SmtcMediaSource::GetAvailableSources(); };

    engine.OnStatusChanged = [&] {
        tray.PostStatusUpdate(L"iTunes-RPC - " + platform_windows::WideFromNarrow(engine.Status()));
    };

    engine.Start();
    int exitCode = tray.RunMessageLoop();
    engine.Stop();

    core::Log::Write("Exiting.");
    return exitCode;
}
