#include "app.hpp"

#include "cli.hpp"
#include "config.hpp"
#include "delete_engine.hpp"
#include "install.hpp"
#include "paths.hpp"
#include "windows_env.hpp"

#include <algorithm>
#include <cctype>
#include <filesystem>
#include <iostream>
#include <string>
#include <vector>

#ifdef _WIN32
  #define EXTERMINATE_WINDOWS 1
#else
  #define EXTERMINATE_WINDOWS 0
#endif

namespace exterminate {

namespace {

std::string style(const std::string& text, const char* ansi_code, bool enabled) {
    if (!enabled) return text;
    return std::string("\x1b[") + ansi_code + "m" + text + "\x1b[0m";
}

std::string normalize_confirmation(std::string value) {
    const auto is_space = [](unsigned char c) { return std::isspace(c) != 0; };
    value.erase(value.begin(), std::find_if(value.begin(), value.end(), [&](unsigned char c) { return !is_space(c); }));
    value.erase(std::find_if(value.rbegin(), value.rend(), [&](unsigned char c) { return !is_space(c); }).base(), value.end());
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) {
        return static_cast<char>(std::toupper(c));
    });
    return value;
}

} // namespace

int run(int argc, char* argv[]) {
#if !EXTERMINATE_WINDOWS
    std::cerr << "exterminate currently supports Windows only.\n";
    return 1;
#else
    const bool standalone = is_standalone_console();
    const bool use_color = has_console_window() && enable_ansi_colors();

    std::vector<std::string> raw_args;
    for (int i = 1; i < argc; ++i) {
        raw_args.emplace_back(argv[i]);
    }

    CliOptions options;
    std::string parse_error;
    if (!parse_cli(argc, argv, options, parse_error)) {
        std::cerr << style("error:", "31;1", use_color) << " " << parse_error << "\n";
        print_usage();
        if (standalone) wait_for_key();
        return 1;
    }

    const std::string base_directory = get_base_directory();
    const AppConfig config = load_config(options.config_path, base_directory);

    if (options.command == Command::Help) {
        print_usage();
        return 0;
    }

    if (options.command == Command::None) {
        if (standalone) {
            const int exit_code = install_self(config, base_directory, options.config_path);
            wait_for_key();
            return exit_code;
        }

        print_usage();
        return 1;
    }

    if (options.command == Command::Install) {
        return install_self(config, base_directory, options.config_path);
    }

    if (options.command == Command::Uninstall) {
        return uninstall_self(config);
    }

    const std::filesystem::path target_path = resolve_target_path(options.target_path);

    if (config.auto_elevate && !options.elevated_run && !is_running_as_admin() && standalone) {
        return relaunch_as_admin(raw_args);
    }

    std::cout << style("Warning: Exterminate permanently deletes targets (no Recycle Bin).", "33;1", use_color) << "\n";

    if (!options.confirmed) {
        if (!has_console_window()) {
            std::cerr << style("error:", "31;1", use_color) << " confirmation required. Re-run with --confirmed.\n";
            return 1;
        }

        std::cout << style("Type YES (or Y) to confirm permanent deletion of:", "36;1", use_color) << "\n";
        std::cout << style(target_path.string(), "36", use_color) << "\n> " << std::flush;

        std::string answer;
        std::getline(std::cin, answer);
        const std::string normalized = normalize_confirmation(answer);
        if (normalized != "YES" && normalized != "Y") {
            std::cout << style("Canceled.", "33;1", use_color) << "\n";
            return 1;
        }
    }

    const DeleteResult result = delete_target(target_path, config);
    if (result.success) {
        std::cout << style(result.message, "32;1", use_color) << "\n";
        return 0;
    }

    std::cerr << style(result.message, "31;1", use_color) << "\n";
    return 1;
#endif
}

} // namespace exterminate
