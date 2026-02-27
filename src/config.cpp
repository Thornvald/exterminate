#include "config.hpp"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <regex>
#include <sstream>

namespace exterminate {

namespace fs = std::filesystem;

namespace {

std::string read_text_file(const fs::path& path) {
    std::ifstream in(path);
    if (!in.is_open()) return "";
    return std::string((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
}

std::string to_lower_copy(std::string value) {
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) {
        return static_cast<char>(std::tolower(c));
    });
    return value;
}

std::string unescape_json_string(const std::string& value) {
    std::string out;
    out.reserve(value.size());
    for (size_t i = 0; i < value.size(); ++i) {
        if (value[i] == '\\' && i + 1 < value.size()) {
            const char next = value[i + 1];
            if (next == '\\' || next == '"' || next == '/') {
                out.push_back(next);
                ++i;
                continue;
            }
            if (next == 'n') {
                out.push_back('\n');
                ++i;
                continue;
            }
            if (next == 'r') {
                out.push_back('\r');
                ++i;
                continue;
            }
            if (next == 't') {
                out.push_back('\t');
                ++i;
                continue;
            }
        }
        out.push_back(value[i]);
    }
    return out;
}

void try_read_int(const std::string& text, const std::string& key, int& target) {
    const std::regex rx("\"" + key + "\"\\s*:\\s*(-?[0-9]+)", std::regex::icase);
    std::smatch match;
    if (std::regex_search(text, match, rx) && match.size() >= 2) {
        try {
            target = std::stoi(match[1].str());
        } catch (...) {
        }
    }
}

void try_read_bool(const std::string& text, const std::string& key, bool& target) {
    const std::regex rx("\"" + key + "\"\\s*:\\s*(true|false)", std::regex::icase);
    std::smatch match;
    if (std::regex_search(text, match, rx) && match.size() >= 2) {
        target = to_lower_copy(match[1].str()) == "true";
    }
}

void try_read_string(const std::string& text, const std::string& key, std::string& target) {
    const std::regex rx("\"" + key + "\"\\s*:\\s*\"([^\"]*)\"", std::regex::icase);
    std::smatch match;
    if (std::regex_search(text, match, rx) && match.size() >= 2) {
        target = unescape_json_string(match[1].str());
    }
}

AppConfig parse_config_text(const std::string& text) {
    AppConfig config;

    try_read_int(text, "retries", config.retries);
    try_read_int(text, "retryDelayMs", config.retry_delay_ms);
    try_read_bool(text, "autoElevate", config.auto_elevate);
    try_read_bool(text, "selfInstallToUserPath", config.self_install_to_user_path);
    try_read_string(text, "installDirectory", config.install_directory);
    try_read_bool(text, "copyDefaultConfigOnInstall", config.copy_default_config_on_install);
    try_read_bool(text, "forceTakeOwnership", config.force_take_ownership);
    try_read_bool(text, "grantAdministratorsFullControl", config.grant_administrators_full_control);
    try_read_bool(text, "grantCurrentUserFullControl", config.grant_current_user_full_control);
    try_read_bool(text, "useRobocopyMirrorFallback", config.use_robocopy_mirror_fallback);
    try_read_bool(text, "useWslFallbackIfAvailable", config.use_wsl_fallback_if_available);

    return config;
}

} // namespace

std::string default_config_path(const std::string& base_directory) {
    return (fs::path(base_directory) / "config" / "exterminate.config.json").string();
}

AppConfig load_config(const std::string& explicit_path, const std::string& base_directory) {
    std::vector<fs::path> candidates;

    if (!explicit_path.empty()) {
        candidates.push_back(fs::path(explicit_path));
    }

    candidates.push_back(fs::path(default_config_path(base_directory)));
    candidates.push_back(fs::path(base_directory) / "exterminate.config.json");

    for (const auto& path : candidates) {
        std::error_code ec;
        if (!fs::exists(path, ec) || ec) continue;

        const std::string text = read_text_file(path);
        if (text.empty()) continue;

        return parse_config_text(text);
    }

    return AppConfig{};
}

} // namespace exterminate
