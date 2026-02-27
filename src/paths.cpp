#include "paths.hpp"

#include <algorithm>
#include <array>
#include <cctype>
#include <fstream>
#include <string>

#ifdef _WIN32
  #define WIN32_LEAN_AND_MEAN
  #define NOMINMAX
  #include <windows.h>
#endif

namespace exterminate {

namespace fs = std::filesystem;

namespace {

std::string trim_copy(std::string value) {
    const auto is_space = [](unsigned char c) { return std::isspace(c) != 0; };
    value.erase(value.begin(), std::find_if(value.begin(), value.end(), [&](unsigned char c) { return !is_space(c); }));
    value.erase(std::find_if(value.rbegin(), value.rend(), [&](unsigned char c) { return !is_space(c); }).base(), value.end());
    return value;
}

} // namespace

fs::path get_executable_path() {
#ifdef _WIN32
    std::array<char, MAX_PATH> buffer{};
    const DWORD length = GetModuleFileNameA(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (length == 0 || length >= buffer.size()) return fs::path();
    return fs::path(std::string(buffer.data(), length));
#else
    return fs::path();
#endif
}

std::string get_base_directory() {
    const fs::path self_path = get_executable_path();
    if (self_path.empty()) return fs::current_path().string();
    return self_path.parent_path().string();
}

std::string get_local_app_data() {
#ifdef _WIN32
    const char* local_app_data = std::getenv("LOCALAPPDATA");
    if (local_app_data && *local_app_data) {
        return std::string(local_app_data);
    }
    const char* user_profile = std::getenv("USERPROFILE");
    if (user_profile && *user_profile) {
        return (fs::path(user_profile) / "AppData" / "Local").string();
    }
    return "C:\\Users\\Default\\AppData\\Local";
#else
    return fs::temp_directory_path().string();
#endif
}

std::string expand_environment_variables(const std::string& value) {
#ifdef _WIN32
    if (value.empty()) return value;
    const DWORD needed = ExpandEnvironmentStringsA(value.c_str(), nullptr, 0);
    if (needed == 0) return value;
    std::string expanded;
    expanded.resize(needed);
    const DWORD written = ExpandEnvironmentStringsA(value.c_str(), expanded.data(), needed);
    if (written == 0) return value;
    if (!expanded.empty() && expanded.back() == '\0') expanded.pop_back();
    return expanded;
#else
    return value;
#endif
}

std::string normalize_path_token(std::string value) {
    value = trim_copy(value);
    if (value.size() >= 2 && value.front() == '"' && value.back() == '"') {
        value = value.substr(1, value.size() - 2);
    }

    std::replace(value.begin(), value.end(), '/', '\\');
    while (!value.empty() && (value.back() == '\\' || value.back() == '/')) {
        value.pop_back();
    }

    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) {
        return static_cast<char>(std::tolower(c));
    });
    return value;
}

bool files_identical(const fs::path& lhs, const fs::path& rhs) {
    std::error_code ec;
    if (!fs::exists(lhs, ec) || !fs::exists(rhs, ec) || ec) return false;
    const auto lhs_size = fs::file_size(lhs, ec);
    if (ec) return false;
    const auto rhs_size = fs::file_size(rhs, ec);
    if (ec || lhs_size != rhs_size) return false;

    std::ifstream a(lhs, std::ios::binary);
    std::ifstream b(rhs, std::ios::binary);
    if (!a.is_open() || !b.is_open()) return false;

    std::array<char, 8192> ab{};
    std::array<char, 8192> bb{};
    while (a && b) {
        a.read(ab.data(), static_cast<std::streamsize>(ab.size()));
        b.read(bb.data(), static_cast<std::streamsize>(bb.size()));
        const auto ac = a.gcount();
        const auto bc = b.gcount();
        if (ac != bc) return false;
        if (ac <= 0) break;
        if (!std::equal(ab.begin(), ab.begin() + ac, bb.begin())) return false;
    }

    return true;
}

fs::path resolve_install_dir(const AppConfig& config) {
    const std::string expanded = expand_environment_variables(config.install_directory);
    std::error_code ec;
    fs::path out = fs::absolute(fs::path(expanded), ec);
    if (ec) return fs::path(expanded);
    return out;
}

fs::path resolve_wrapper_bin_dir(const AppConfig& config) {
    return resolve_install_dir(config) / "bin";
}

fs::path resolve_installed_exe(const AppConfig& config) {
    return resolve_install_dir(config) / "exterminate.exe";
}

fs::path resolve_target_path(const std::string& input) {
    std::string value = trim_copy(input);
    if (value.size() >= 2 && value.front() == '"' && value.back() == '"') {
        value = value.substr(1, value.size() - 2);
    }

    value = expand_environment_variables(value);
    fs::path path = fs::path(value);
    if (!path.is_absolute()) {
        path = fs::current_path() / path;
    }

    std::error_code ec;
    path = fs::weakly_canonical(path, ec);
    if (!ec) return path;

    ec.clear();
    path = fs::absolute(path, ec);
    return path;
}

std::string to_verbatim_path(const fs::path& path) {
    const std::string raw = path.string();
    if (raw.rfind("\\\\?\\", 0) == 0) return raw;
    if (raw.rfind("\\\\", 0) == 0) return "\\\\?\\UNC\\" + raw.substr(2);
    return "\\\\?\\" + raw;
}

bool try_to_wsl_path(const fs::path& path, std::string& out_wsl_path) {
    const std::string raw = path.string();
    if (raw.size() < 3) return false;
    if (!std::isalpha(static_cast<unsigned char>(raw[0])) || raw[1] != ':' || (raw[2] != '\\' && raw[2] != '/')) {
        return false;
    }

    const char drive = static_cast<char>(std::tolower(raw[0]));
    std::string rest = raw.substr(3);
    std::replace(rest.begin(), rest.end(), '\\', '/');
    if (rest.empty()) {
        out_wsl_path = std::string("/mnt/") + drive + "/";
    } else {
        out_wsl_path = std::string("/mnt/") + drive + "/" + rest;
    }
    return true;
}

} // namespace exterminate
