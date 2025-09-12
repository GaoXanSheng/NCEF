cmake_minimum_required(VERSION 3.20)
project(SpoutDXWrapper LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(MSVC)
    string(REPLACE "/RTC1" "" CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG}")
    string(REPLACE "/RTC1" "" CMAKE_C_FLAGS_DEBUG "${CMAKE_C_FLAGS_DEBUG}")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /clr")
endif()

# 源文件
set(SOURCES
        SpoutDXWrapper.cpp
)

# 头文件
set(HEADERS
        SpoutDXWrapper.h
        Spout/SpoutDX.h
        Spout/SpoutCommon.h
        Spout/SpoutCopy.h
        Spout/SpoutDirectX.h
        Spout/SpoutFrameCount.h
        Spout/SpoutSenderNames.h
        Spout/SpoutSharedMemory.h
        Spout/SpoutUtils.h
)

include_directories(${CMAKE_CURRENT_SOURCE_DIR})

set(WINDOWS_SDK_LIB_PATH "C:/Program Files (x86)/Windows Kits/10/Lib/10.0.26100.0/um/x64")

find_library(D3D11_LIB d3d11 PATHS ${WINDOWS_SDK_LIB_PATH})
find_library(DXGI_LIB dxgi PATHS ${WINDOWS_SDK_LIB_PATH})

message(STATUS "D3D11_LIB=${D3D11_LIB}")
message(STATUS "DXGI_LIB=${DXGI_LIB}")

set(SPOUTDX_LIB "${CMAKE_CURRENT_SOURCE_DIR}/Spout/libs/SpoutDX.lib")
message(STATUS "SPOUTDX_LIB=${SPOUTDX_LIB}")

add_library(SpoutDXWrapper SHARED ${SOURCES} ${HEADERS})

target_link_libraries(SpoutDXWrapper PRIVATE
        ${SPOUTDX_LIB}
        ${D3D11_LIB}
        ${DXGI_LIB}
)

set_target_properties(SpoutDXWrapper PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_CURRENT_SOURCE_DIR}/bin/Debug
        RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_CURRENT_SOURCE_DIR}/bin/Release
)

if(MSVC)
    target_compile_definitions(SpoutDXWrapper PRIVATE SPOUTDXWRAPPER_EXPORTS)
endif()
