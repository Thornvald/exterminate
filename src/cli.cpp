#include "cli.hpp"

#include <algorithm>
#include <cctype>
#include <iostream>
#include <vector>

namespace exterminate {

namespace {

std::string to_lower_copy(const std::string& value) {
    std::string out = value;
    std::transform(out.begin(), out.end(), out.begin(), [](unsigned char c) {
        return static_cast<char>(std::tolower(c));
    });
    return out;
}

bool read_next_value(int argc, char* argv[], int& index, std::string& out_value) {
    const int next_index = index + 1;
    if (next_index >= argc) return false;
    out_value = argv[next_index];
    index = next_index;
    return true;
}

bool looks_like_option(const std::string& value) {
    return value.size() > 1 && (value.front() == '-' || value.front() == '/');
}

} // namespace

bool parse_cli(int argc, char* argv[], CliOptions& out_options, std::string& out_error) {
    out_options = CliOptions{};
    out_error.clear();

    std::vector<std::string> target_parts;
    bool install = false;
    bool uninstall = false;
    bool help = false;

    for (int index = 1; index < argc; ++index) {
        const std::string argument = argv[index];
        const std::string normalized = to_lower_copy(argument);

        if (normalized == "--install" || normalized == "-install" || normalized == "/install") {
            install = true;
            continue;
        }

        if (normalized == "--uninstall" || normalized == "-uninstall" || normalized == "/uninstall") {
            uninstall = true;
            continue;
        }

        if (normalized == "--help" || normalized == "-h" || normalized == "/?") {
            help = true;
            continue;
        }

        if (normalized == "--config" || normalized == "-config" || normalized == "/config") {
            if (!read_next_value(argc, argv, index, out_options.config_path)) {
                out_error = "missing value for --config";
                return false;
            }
            continue;
        }

        if (normalized == "--elevated-run") {
            out_options.elevated_run = true;
            continue;
        }

        if (normalized == "--confirmed" || normalized == "--yes" || normalized == "-y") {
            out_options.confirmed = true;
            continue;
        }

        if (looks_like_option(argument)) {
            out_error = "unknown option: " + argument;
            return false;
        }

        target_parts.push_back(argument);
    }

    if (help) {
        out_options.command = Command::Help;
        return true;
    }

    if (install && uninstall) {
        out_error = "use either install or uninstall, not both";
        return false;
    }

    if (install) {
        if (!target_parts.empty()) {
            out_error = "install mode does not accept a target path";
            return false;
        }
        out_options.command = Command::Install;
        return true;
    }

    if (uninstall) {
        if (!target_parts.empty()) {
            out_error = "uninstall mode does not accept a target path";
            return false;
        }
        out_options.command = Command::Uninstall;
        return true;
    }

    if (!target_parts.empty()) {
        std::string merged_target;
        for (size_t i = 0; i < target_parts.size(); ++i) {
            if (i > 0) merged_target.push_back(' ');
            merged_target += target_parts[i];
        }

        out_options.command = Command::Delete;
        out_options.target_path = merged_target;
        return true;
    }

    out_options.command = Command::None;
    return true;
}

void print_usage() {
    std::cout << "Usage:\n";
    std::cout << "  exterminate \"C:\\path\\to\\target\"\n";
    std::cout << "  exterminate --install\n";
    std::cout << "  exterminate -install\n";
    std::cout << "  exterminate --uninstall\n";
    std::cout << "  exterminate -uninstall\n";
    std::cout << "  exterminate --confirmed \"C:\\path\\to\\target\"\n";
    std::cout << "  exterminate --config \"C:\\path\\to\\config.json\" \"C:\\path\\to\\target\"\n";
    std::cout << "\nWarning: Exterminate permanently deletes targets (no Recycle Bin).\n";
}

} // namespace exterminate
