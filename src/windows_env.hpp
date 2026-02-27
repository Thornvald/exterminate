#pragma once

#include <string>
#include <vector>

namespace exterminate {

bool is_running_as_admin();
int relaunch_as_admin(const std::vector<std::string>& args);

bool is_standalone_console();
bool has_console_window();
bool enable_ansi_colors();
void wait_for_key();

int run_hidden_process(const std::string& file_name, const std::vector<std::string>& args);
bool command_exists_on_path(const std::string& command_name);

bool ensure_user_path_entry(const std::string& entry);
bool remove_user_path_entry(const std::string& entry);
void broadcast_environment_change();

} // namespace exterminate
