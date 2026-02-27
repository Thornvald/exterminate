#pragma once

#include <string>

namespace exterminate {

enum class Command {
    None,
    Delete,
    Install,
    Uninstall,
    Help,
};

struct CliOptions {
    Command command = Command::None;
    std::string target_path;
    std::string config_path;
    bool elevated_run = false;
    bool confirmed = false;
};

bool parse_cli(int argc, char* argv[], CliOptions& out_options, std::string& out_error);
void print_usage();

} // namespace exterminate
