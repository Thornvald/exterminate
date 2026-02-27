#include "windows_env.hpp"

#include "paths.hpp"

#include <algorithm>
#include <array>
#include <cctype>
#include <filesystem>
#include <iostream>
#include <sstream>

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <shellapi.h>

namespace exterminate {

namespace {

bool enable_ansi_for_handle(DWORD std_handle_id) {
    HANDLE handle = GetStdHandle(std_handle_id);
    if (handle == nullptr || handle == INVALID_HANDLE_VALUE) return false;

    DWORD mode = 0;
    if (!GetConsoleMode(handle, &mode)) return false;

    const DWORD target_mode = mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING;
    if (target_mode == mode) return true;
    return SetConsoleMode(handle, target_mode) != FALSE;
}

std::string quote_argument(const std::string& value) {
    if (value.empty()) return "\"\"";
    if (value.find_first_of(" \t\"") == std::string::npos) return value;

    std::string out;
    out.push_back('"');
    int slash_count = 0;
    for (char c : value) {
        if (c == '\\') {
            ++slash_count;
            continue;
        }

        if (c == '"') {
            out.append(static_cast<size_t>(slash_count * 2 + 1), '\\');
            out.push_back('"');
            slash_count = 0;
            continue;
        }

        out.append(static_cast<size_t>(slash_count), '\\');
        slash_count = 0;
        out.push_back(c);
    }
    out.append(static_cast<size_t>(slash_count * 2), '\\');
    out.push_back('"');
    return out;
}

std::string normalize_path_piece(std::string value) {
    value = normalize_path_token(std::move(value));
    return value;
}

std::vector<std::string> split_path_list(const std::string& value) {
    std::vector<std::string> out;
    std::istringstream ss(value);
    std::string token;
    while (std::getline(ss, token, ';')) {
        if (token.empty()) continue;
        out.push_back(token);
    }
    return out;
}

std::string join_path_list(const std::vector<std::string>& values) {
    std::string out;
    for (size_t i = 0; i < values.size(); ++i) {
        if (i > 0) out.push_back(';');
        out += values[i];
    }
    return out;
}

std::string read_user_path_registry() {
    HKEY key = nullptr;
    if (RegOpenKeyExA(HKEY_CURRENT_USER, "Environment", 0, KEY_READ, &key) != ERROR_SUCCESS) {
        return "";
    }

    char buffer[32767];
    DWORD size = sizeof(buffer);
    DWORD type = 0;
    std::string result;

    if (RegQueryValueExA(key, "Path", nullptr, &type, reinterpret_cast<LPBYTE>(buffer), &size) == ERROR_SUCCESS) {
        if (size > 0) result.assign(buffer, size > 1 ? size - 1 : 0);
    }
    RegCloseKey(key);
    return result;
}

void write_user_path_registry(const std::string& value) {
    HKEY key = nullptr;
    if (RegOpenKeyExA(HKEY_CURRENT_USER, "Environment", 0, KEY_WRITE, &key) != ERROR_SUCCESS) {
        return;
    }

    RegSetValueExA(key, "Path", 0, REG_EXPAND_SZ,
                   reinterpret_cast<const BYTE*>(value.c_str()),
                   static_cast<DWORD>(value.size() + 1));
    RegCloseKey(key);
}

} // namespace

bool is_running_as_admin() {
    BOOL is_member = FALSE;
    SID_IDENTIFIER_AUTHORITY nt_authority = SECURITY_NT_AUTHORITY;
    PSID admin_group = nullptr;
    if (!AllocateAndInitializeSid(&nt_authority, 2,
                                  SECURITY_BUILTIN_DOMAIN_RID,
                                  DOMAIN_ALIAS_RID_ADMINS,
                                  0, 0, 0, 0, 0, 0,
                                  &admin_group)) {
        return false;
    }

    if (!CheckTokenMembership(nullptr, admin_group, &is_member)) {
        is_member = FALSE;
    }
    FreeSid(admin_group);
    return is_member == TRUE;
}

int relaunch_as_admin(const std::vector<std::string>& args) {
    const std::string self = get_executable_path().string();
    if (self.empty()) {
        std::cerr << "error: could not locate executable path for elevation.\n";
        return 1;
    }

    std::vector<std::string> elevated_args = args;
    if (std::find(elevated_args.begin(), elevated_args.end(), "--elevated-run") == elevated_args.end()) {
        elevated_args.push_back("--elevated-run");
    }

    std::string parameters;
    for (size_t i = 0; i < elevated_args.size(); ++i) {
        if (i > 0) parameters.push_back(' ');
        parameters += quote_argument(elevated_args[i]);
    }

    SHELLEXECUTEINFOA info{};
    info.cbSize = sizeof(info);
    info.fMask = SEE_MASK_NOCLOSEPROCESS;
    info.lpVerb = "runas";
    info.lpFile = self.c_str();
    info.lpParameters = parameters.c_str();
    info.nShow = SW_SHOWNORMAL;

    if (!ShellExecuteExA(&info)) {
        const DWORD error_code = GetLastError();
        if (error_code == ERROR_CANCELLED) {
            std::cerr << "elevation canceled.\n";
            return 1;
        }
        std::cerr << "error: failed to relaunch as administrator.\n";
        return 1;
    }

    WaitForSingleObject(info.hProcess, INFINITE);
    DWORD exit_code = 1;
    GetExitCodeProcess(info.hProcess, &exit_code);
    CloseHandle(info.hProcess);
    return static_cast<int>(exit_code);
}

bool is_standalone_console() {
    std::array<DWORD, 2> ids{};
    const DWORD count = GetConsoleProcessList(ids.data(), static_cast<DWORD>(ids.size()));
    return count == 1;
}

bool has_console_window() {
    return GetConsoleWindow() != nullptr;
}

bool enable_ansi_colors() {
    const bool stdout_ok = enable_ansi_for_handle(STD_OUTPUT_HANDLE);
    const bool stderr_ok = enable_ansi_for_handle(STD_ERROR_HANDLE);
    return stdout_ok || stderr_ok;
}

void wait_for_key() {
    std::cout << "\nPress any key to close...\n";
    std::cin.get();
}

int run_hidden_process(const std::string& file_name, const std::vector<std::string>& args) {
    std::string command_line = quote_argument(file_name);
    for (const auto& arg : args) {
        command_line.push_back(' ');
        command_line += quote_argument(arg);
    }

    STARTUPINFOA startup{};
    PROCESS_INFORMATION process{};
    startup.cb = sizeof(startup);

    std::vector<char> mutable_cmd(command_line.begin(), command_line.end());
    mutable_cmd.push_back('\0');

    const BOOL started = CreateProcessA(
        nullptr,
        mutable_cmd.data(),
        nullptr,
        nullptr,
        FALSE,
        CREATE_NO_WINDOW,
        nullptr,
        nullptr,
        &startup,
        &process);

    if (!started) return -1;

    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD exit_code = 1;
    GetExitCodeProcess(process.hProcess, &exit_code);
    CloseHandle(process.hProcess);
    CloseHandle(process.hThread);
    return static_cast<int>(exit_code);
}

bool command_exists_on_path(const std::string& command_name) {
    const char* path_env = std::getenv("PATH");
    if (!path_env || !*path_env) return false;

    const char* pathext_env = std::getenv("PATHEXT");
    std::vector<std::string> extensions;
    if (pathext_env && *pathext_env) {
        std::istringstream ss(pathext_env);
        std::string token;
        while (std::getline(ss, token, ';')) {
            if (!token.empty()) extensions.push_back(token);
        }
    }
    if (extensions.empty()) {
        extensions = {"", ".exe", ".cmd", ".bat"};
    }

    std::istringstream path_stream(path_env);
    std::string directory;
    while (std::getline(path_stream, directory, ';')) {
        if (directory.empty()) continue;
        for (const auto& ext : extensions) {
            std::string candidate_name = command_name;
            const std::string lower_candidate = normalize_path_piece(candidate_name);
            const std::string lower_ext = normalize_path_piece(ext);
            if (!ext.empty() && (lower_candidate.size() < lower_ext.size() ||
                                 lower_candidate.substr(lower_candidate.size() - lower_ext.size()) != lower_ext)) {
                candidate_name += ext;
            }

            const auto candidate = std::filesystem::path(directory) / candidate_name;
            std::error_code ec;
            if (std::filesystem::exists(candidate, ec) && !ec) return true;
        }
    }
    return false;
}

bool ensure_user_path_entry(const std::string& entry) {
    const std::string normalized_entry = normalize_path_piece(entry);
    if (normalized_entry.empty()) return false;

    std::string user_path = read_user_path_registry();
    auto tokens = split_path_list(user_path);

    for (const auto& token : tokens) {
        if (normalize_path_piece(token) == normalized_entry) return false;
    }

    tokens.push_back(entry);
    write_user_path_registry(join_path_list(tokens));
    return true;
}

bool remove_user_path_entry(const std::string& entry) {
    const std::string normalized_entry = normalize_path_piece(entry);
    if (normalized_entry.empty()) return false;

    std::string user_path = read_user_path_registry();
    auto tokens = split_path_list(user_path);

    std::vector<std::string> kept;
    kept.reserve(tokens.size());
    bool removed = false;

    for (const auto& token : tokens) {
        if (normalize_path_piece(token) == normalized_entry) {
            removed = true;
            continue;
        }
        kept.push_back(token);
    }

    if (!removed) return false;
    write_user_path_registry(join_path_list(kept));
    return true;
}

void broadcast_environment_change() {
    DWORD_PTR result = 0;
    SendMessageTimeoutA(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
                        reinterpret_cast<LPARAM>("Environment"),
                        SMTO_ABORTIFHUNG, 5000, &result);
}

} // namespace exterminate
