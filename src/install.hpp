#pragma once

#include <string>

#include "config.hpp"

namespace exterminate {

int install_self(const AppConfig& config, const std::string& base_directory, const std::string& config_source_path);
int uninstall_self(const AppConfig& config);

} // namespace exterminate
