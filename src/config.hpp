#pragma once

#include <string>

namespace exterminate {

struct AppConfig {
    int retries = 6;
    int retry_delay_ms = 350;
    bool auto_elevate = true;
    bool self_install_to_user_path = true;
    std::string install_directory = "%LOCALAPPDATA%\\Exterminate";
    bool copy_default_config_on_install = true;
    bool force_take_ownership = true;
    bool grant_administrators_full_control = true;
    bool grant_current_user_full_control = true;
    bool use_robocopy_mirror_fallback = true;
    bool use_wsl_fallback_if_available = true;
};

AppConfig load_config(const std::string& explicit_path, const std::string& base_directory);
std::string default_config_path(const std::string& base_directory);

} // namespace exterminate
