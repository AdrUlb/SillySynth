cmake_minimum_required(VERSION 4.1.0)

project(SillySynthNativeTest C)

file(GLOB_RECURSE SOURCES CONFIGURE_DEPENDS "Source/*.c" "Source/*.h")
add_executable(SillySynthNativeTest ${SOURCES})
target_link_libraries(SillySynthNativeTest PRIVATE SDL3 SillySynth)
target_link_directories(SillySynthNativeTest PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})