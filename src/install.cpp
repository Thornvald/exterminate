#include "install.hpp"

#include "paths.hpp"
#include "windows_env.hpp"

#include <filesystem>
#include <fstream>
#include <iostream>
#include <cstdlib>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

namespace exterminate {

namespace fs = std::filesystem;

namespace {

enum class InstallState {
    Installed,
    Updated,
    AlreadyInstalled,
};

void write_text_file(const fs::path& path, const std::string& text) {
    std::ofstream out(path, std::ios::binary | std::ios::trunc);
    if (!out.is_open()) return;
    out << text;
}

std::string install_state_text(InstallState state) {
    switch (state) {
        case InstallState::Installed: return "installed";
        case InstallState::Updated: return "updated";
        case InstallState::AlreadyInstalled: return "already installed";
        default: return "installed";
    }
}

void remove_legacy_wrappers(const fs::path& install_dir) {
    std::error_code ec;
    fs::remove(install_dir / "ex.cmd", ec);
    ec.clear();
    fs::remove(install_dir / "exterminate.cmd", ec);
    ec.clear();
    fs::remove_all(install_dir / "bin", ec);
}

void write_context_script(const fs::path& install_dir) {
    const std::string script =
        "Set shell = CreateObject(\"WScript.Shell\")\r\n"
        "Set fso = CreateObject(\"Scripting.FileSystemObject\")\r\n"
        "basePath = fso.GetParentFolderName(WScript.ScriptFullName)\r\n"
        "exePath = fso.BuildPath(basePath, \"exterminate.exe\")\r\n"
        "If WScript.Arguments.Count = 0 Then WScript.Quit 1\r\n"
        "targetPath = WScript.Arguments.Item(0)\r\n"
        "confirm = MsgBox(\"Warning: Exterminate permanently deletes targets (no Recycle Bin).\" & vbCrLf & vbCrLf & \"Delete this target?\" & vbCrLf & targetPath, vbYesNo + vbExclamation + vbDefaultButton2, \"Exterminate\")\r\n"
        "If confirm <> vbYes Then WScript.Quit 0\r\n"
        "command = Chr(34) & exePath & Chr(34) & \" --confirmed --elevated-run \" & Chr(34) & targetPath & Chr(34)\r\n"
        "exitCode = shell.Run(command, 0, True)\r\n"
        "WScript.Quit exitCode\r\n";

    write_text_file(install_dir / "exterminate-context.vbs", script);
}

void copy_default_config(const AppConfig& config, const std::string& base_directory, const std::string& explicit_source) {
    if (!config.copy_default_config_on_install) return;

    fs::path source;
    if (!explicit_source.empty()) {
        source = fs::path(explicit_source);
    } else {
        source = fs::path(default_config_path(base_directory));
    }

    std::error_code ec;
    if (!fs::exists(source, ec) || ec) return;

    const fs::path destination_dir = resolve_install_dir(config) / "config";
    fs::create_directories(destination_dir, ec);
    if (ec) return;

    fs::copy_file(source, destination_dir / "exterminate.config.json", fs::copy_options::overwrite_existing, ec);
}

bool running_inside_path(const fs::path& root, const fs::path& path) {
    const std::string normalized_root = normalize_path_token(root.string());
    const std::string normalized_path = normalize_path_token(path.string());
    if (normalized_root.empty() || normalized_path.empty()) return false;
    if (normalized_path.size() < normalized_root.size()) return false;
    return normalized_path.rfind(normalized_root, 0) == 0;
}

bool schedule_self_uninstall(const fs::path& install_dir) {
    const fs::path temp_script = fs::temp_directory_path() /
                                 ("exterminate-uninstall-" + std::to_string(GetTickCount64()) + ".cmd");
    const std::string script =
        "@echo off\r\n"
        "set \"TARGET=%~1\"\r\n"
        "for /l %%I in (1,1,240) do (\r\n"
        "  rmdir /s /q \"%TARGET%\" >nul 2>nul\r\n"
        "  if not exist \"%TARGET%\" goto done\r\n"
        "  ping 127.0.0.1 -n 2 >nul\r\n"
        ")\r\n"
        ":done\r\n"
        "del /f /q \"%~f0\" >nul 2>nul\r\n";

    write_text_file(temp_script, script);
    if (!fs::exists(temp_script)) return false;

    const std::string args =
        "/d /c \"\"" + temp_script.string() + "\" \"" + install_dir.string() + "\"\"";

    const char* comspec = std::getenv("ComSpec");
    const std::string cmd_path = (comspec && *comspec) ? std::string(comspec) : std::string("C:\\Windows\\System32\\cmd.exe");

    STARTUPINFOA startup{};
    PROCESS_INFORMATION process{};
    startup.cb = sizeof(startup);

    std::vector<char> mutable_args(args.begin(), args.end());
    mutable_args.push_back('\0');

    const BOOL started = CreateProcessA(
        cmd_path.c_str(),
        mutable_args.data(),
        nullptr,
        nullptr,
        FALSE,
        DETACHED_PROCESS | CREATE_NO_WINDOW,
        nullptr,
        nullptr,
        &startup,
        &process);

    if (!started) return false;

    CloseHandle(process.hProcess);
    CloseHandle(process.hThread);
    return true;
}

} // namespace

int install_self(const AppConfig& config, const std::string& base_directory, const std::string& config_source_path) {
    const fs::path self_path = get_executable_path();
    if (self_path.empty() || !fs::exists(self_path)) {
        std::cerr << "error: could not resolve current executable path.\n";
        return 1;
    }

    const fs::path install_dir = resolve_install_dir(config);
    const fs::path wrapper_dir = resolve_wrapper_bin_dir(config);
    const fs::path install_exe = resolve_installed_exe(config);

    std::error_code ec;
    fs::create_directories(install_dir, ec);
    if (ec) {
        std::cerr << "error: could not create install directory: " << install_dir << "\n";
        return 1;
    }

    InstallState state = InstallState::Installed;
    if (!files_identical(self_path, install_exe)) {
        if (fs::exists(install_exe, ec) && !ec) {
            state = InstallState::Updated;
        }
        fs::copy_file(self_path, install_exe, fs::copy_options::overwrite_existing, ec);
        if (ec) {
            std::cerr << "error: could not copy executable to install directory.\n";
            return 1;
        }
    } else {
        state = InstallState::AlreadyInstalled;
    }

    write_context_script(install_dir);
    remove_legacy_wrappers(install_dir);
    copy_default_config(config, base_directory, config_source_path);

    remove_user_path_entry(wrapper_dir.string());
    if (config.self_install_to_user_path) {
        if (ensure_user_path_entry(install_dir.string())) {
            std::cout << "Added to PATH: " << install_dir.string() << "\n";
        }
    }
    broadcast_environment_change();

    std::cout << "Installed path: " << install_exe.string() << "\n";
    std::cout << "Status: " << install_state_text(state) << "\n";
    std::cout << "Context script: " << (install_dir / "exterminate-context.vbs").string() << "\n";
    std::cout << "Open a new terminal and run: exterminate \"C:\\path\\to\\target\"\n";
    std::cout << "Warning: Exterminate permanently deletes targets (no Recycle Bin).\n";
    return 0;
}

int uninstall_self(const AppConfig& config) {
    const fs::path install_dir = resolve_install_dir(config);
    const fs::path wrapper_dir = resolve_wrapper_bin_dir(config);

    remove_user_path_entry(wrapper_dir.string());
    remove_user_path_entry(install_dir.string());
    broadcast_environment_change();

    std::error_code ec;
    if (!fs::exists(install_dir, ec) || ec) {
        std::cout << "Uninstalled exterminate.\n";
        return 0;
    }

    const fs::path self_path = get_executable_path();
    if (!self_path.empty() && running_inside_path(install_dir, self_path)) {
        if (!schedule_self_uninstall(install_dir)) {
            std::cerr << "error: failed to schedule uninstall cleanup.\n";
            return 1;
        }

        std::cout << "Uninstalled exterminate.\n";
        return 0;
    }

    fs::remove_all(install_dir, ec);
    if (ec) {
        std::cerr << "error: failed to remove install directory.\n";
        std::cerr << "You can remove it manually: " << install_dir.string() << "\n";
        return 1;
    }

    std::cout << "Uninstalled exterminate.\n";
    return 0;
}

} // namespace exterminate
