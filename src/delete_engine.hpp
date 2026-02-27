#pragma once

#include <filesystem>
#include <string>

#include "config.hpp"

namespace exterminate {

struct DeleteResult {
    bool success = false;
    bool already_gone = false;
    std::string message;
};

DeleteResult delete_target(const std::filesystem::path& target_path, const AppConfig& config);

} // namespace exterminate
