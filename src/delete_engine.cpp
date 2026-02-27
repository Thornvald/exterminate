#include "delete_engine.hpp"

#include "paths.hpp"
#include "windows_env.hpp"

#include <chrono>
#include <filesystem>
#include <thread>
#include <vector>

namespace exterminate {

namespace fs = std::filesystem;

namespace {

bool path_exists(const fs::path& path) {
    std::error_code ec;
    return fs::exists(path, ec) && !ec;
}

bool path_is_directory(const fs::path& path) {
    std::error_code ec;
    return fs::is_directory(path, ec) && !ec;
}

void clear_attributes(const fs::path& path, bool directory) {
    if (directory) {
        run_hidden_process("attrib.exe", {"-R", "-S", "-H", path.string(), "/S", "/D"});
    } else {
        run_hidden_process("attrib.exe", {"-R", "-S", "-H", path.string()});
    }
}

void take_ownership(const fs::path& path, bool directory) {
    if (directory) {
        run_hidden_process("takeown.exe", {"/F", path.string(), "/A", "/R", "/D", "Y"});
    } else {
        run_hidden_process("takeown.exe", {"/F", path.string(), "/A", "/D", "Y"});
    }
}

void grant_admin_full_control(const fs::path& path, bool directory) {
    if (directory) {
        run_hidden_process("icacls.exe", {path.string(), "/grant", "*S-1-5-32-544:(OI)(CI)F", "/T", "/C"});
    } else {
        run_hidden_process("icacls.exe", {path.string(), "/grant", "*S-1-5-32-544:F", "/C"});
    }
}

void grant_current_user_full_control(const fs::path& path, bool directory) {
    const char* user_domain = std::getenv("USERDOMAIN");
    const char* user_name = std::getenv("USERNAME");
    if (!user_name || !*user_name) return;

    std::string identity;
    if (user_domain && *user_domain) {
        identity = std::string(user_domain) + "\\" + user_name;
    } else {
        identity = user_name;
    }

    if (directory) {
        run_hidden_process("icacls.exe", {path.string(), "/grant", identity + ":(OI)(CI)F", "/T", "/C"});
    } else {
        run_hidden_process("icacls.exe", {path.string(), "/grant", identity + ":F", "/C"});
    }
}

void delete_with_std_filesystem(const fs::path& path, bool directory) {
    std::error_code ec;
    if (directory) {
        fs::remove_all(path, ec);
    } else {
        fs::remove(path, ec);
    }
}

void delete_with_cmd(const fs::path& path, bool directory) {
    const std::string verbatim = to_verbatim_path(path);
    if (directory) {
        run_hidden_process("cmd.exe", {"/d", "/c", "rd /s /q \"" + verbatim + "\""});
    } else {
        run_hidden_process("cmd.exe", {"/d", "/c", "del /f /q \"" + verbatim + "\""});
    }
}

void delete_with_robocopy(const fs::path& path) {
    const auto tick = std::chrono::steady_clock::now().time_since_epoch().count();
    const fs::path temp = fs::temp_directory_path() / ("exterminate-empty-" + std::to_string(tick));
    std::error_code ec;
    fs::create_directories(temp, ec);
    if (ec) return;

    run_hidden_process("robocopy.exe", {
        temp.string(),
        path.string(),
        "/MIR",
        "/NFL",
        "/NDL",
        "/NJH",
        "/NJS",
        "/NP",
        "/R:0",
        "/W:0",
    });

    delete_with_cmd(path, true);
    delete_with_std_filesystem(path, true);

    fs::remove_all(temp, ec);
}

void delete_with_wsl(const fs::path& path) {
    if (!command_exists_on_path("wsl.exe")) return;
    std::string wsl_path;
    if (!try_to_wsl_path(path, wsl_path)) return;
    run_hidden_process("wsl.exe", {"--exec", "rm", "-rf", "--", wsl_path});
}

} // namespace

DeleteResult delete_target(const fs::path& target_path, const AppConfig& config) {
    if (!path_exists(target_path)) {
        return DeleteResult{true, true, "Already gone: " + target_path.string()};
    }

    int retries = config.retries;
    if (retries < 0) retries = 0;
    int retry_delay_ms = config.retry_delay_ms;
    if (retry_delay_ms < 0) retry_delay_ms = 0;

    for (int attempt = 0; attempt <= retries; ++attempt) {
        if (!path_exists(target_path)) break;

        const bool directory = path_is_directory(target_path);

        clear_attributes(target_path, directory);

        if (config.force_take_ownership) {
            take_ownership(target_path, directory);
        }

        if (config.grant_administrators_full_control) {
            grant_admin_full_control(target_path, directory);
        }

        if (config.grant_current_user_full_control) {
            grant_current_user_full_control(target_path, directory);
        }

        delete_with_std_filesystem(target_path, directory);
        if (path_exists(target_path)) {
            delete_with_cmd(target_path, directory);
        }

        if (config.use_robocopy_mirror_fallback && directory && path_exists(target_path)) {
            delete_with_robocopy(target_path);
        }

        if (config.use_wsl_fallback_if_available && path_exists(target_path)) {
            delete_with_wsl(target_path);
        }

        if (!path_exists(target_path)) {
            return DeleteResult{true, false, "Deleted: " + target_path.string()};
        }

        if (attempt < retries) {
            std::this_thread::sleep_for(std::chrono::milliseconds(retry_delay_ms));
        }
    }

    return DeleteResult{false, false, "Failed to delete: " + target_path.string()};
}

} // namespace exterminate
