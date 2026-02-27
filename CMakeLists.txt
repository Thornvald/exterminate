cmake_minimum_required(VERSION 3.16)
project(exterminate VERSION 0.3.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(MSVC)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    add_compile_options(/W4 /utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS)
elseif(MINGW)
    add_link_options(-static -static-libgcc -static-libstdc++)
endif()

add_executable(
    exterminate
    src/main.cpp
    src/app.cpp
    src/cli.cpp
    src/config.cpp
    src/delete_engine.cpp
    src/install.cpp
    src/paths.cpp
    src/windows_env.cpp
)

if(WIN32)
    target_link_libraries(exterminate PRIVATE advapi32 user32 shell32)
endif()

install(TARGETS exterminate RUNTIME DESTINATION .)
