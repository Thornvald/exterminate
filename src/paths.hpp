#pragma once

#include <filesystem>
#include <string>

#include "config.hpp"

namespace exterminate {

std::filesystem::path get_executable_path();
std::string get_base_directory();
std::string get_local_app_data();

std::string expand_environment_variables(const std::string& value);
std::string normalize_path_token(std::string value);
bool files_identical(const std::filesystem::path& lhs, const std::filesystem::path& rhs);

std::filesystem::path resolve_install_dir(const AppConfig& config);
std::filesystem::path resolve_wrapper_bin_dir(const AppConfig& config);
std::filesystem::path resolve_installed_exe(const AppConfig& config);

std::filesystem::path resolve_target_path(const std::string& input);
std::string to_verbatim_path(const std::filesystem::path& path);
bool try_to_wsl_path(const std::filesystem::path& path, std::string& out_wsl_path);

} // namespace exterminate
