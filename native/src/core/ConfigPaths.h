#pragma once

#include <filesystem>

namespace core {

// Per-OS config/log directory resolution lives in platform/ (each
// platform's rule for "where does user config belong" is genuinely
// different - not a portability gap to abstract away further).
std::filesystem::path GetConfigDirectory();
std::filesystem::path GetConfigFilePath();
std::filesystem::path GetLogFilePath();

} // namespace core
