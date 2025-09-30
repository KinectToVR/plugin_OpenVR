# CMakeList.txt : CMake project for driver_Amethyst, include source and define
# project specific logic here.
#

option(USE_HOOKS "Enable hooks" OFF)

if(USE_HOOKS)
    add_compile_definitions(USE_HOOKS=1)
    message(STATUS "USE_HOOKS enabled")
endif()

file(GLOB driver_HDR "include/*.h")
file(GLOB driver_SRC "src/*.cpp")
file(GLOB driver_IDL "include/*.idl")

source_group(TREE "${CMAKE_CURRENT_SOURCE_DIR}/include" PREFIX "Header Files" FILES ${driver_HDR})
source_group(TREE "${CMAKE_CURRENT_SOURCE_DIR}/src"     PREFIX "Source Files" FILES ${driver_SRC})
source_group(TREE "${CMAKE_CURRENT_SOURCE_DIR}/include" PREFIX "IDL Files"    FILES ${driver_IDL})

set_source_files_properties(${driver_IDL} PROPERTIES HEADER_FILE_ONLY TRUE)

# Define target name based on USE_HOOKS
if(USE_HOOKS)
    set(DRIVER_TARGET_NAME driver_00Amethyst)
else()
    set(DRIVER_TARGET_NAME driver_Amethyst)
endif()

if(MSVC)
    add_compile_options(
        $<$<CONFIG:>:/MT>
        $<$<CONFIG:Debug>:/MTd>
        $<$<CONFIG:Release>:/MT>
    )
endif()

# ----------------------- MinHook support ----------------------
if (WIN32 AND USE_HOOKS)
    find_package(minhook CONFIG REQUIRED)
    set(MINHOOK_LIB minhook::minhook)
endif()
# --------------------------------------------------------------

# ----------------------- Cap'n Proto --------------------------
find_package(CapnProto CONFIG REQUIRED)

# Auto-generate sources from rpc/driver.capnp
set(CAPNP_SCHEMA "${CMAKE_CURRENT_SOURCE_DIR}/../vendor/k2vr/driver.capnp")
set(CAPNP_OUT_DIR "${CMAKE_CURRENT_BINARY_DIR}/generated_files/capnp")
set(CAPNP_SCHEMA_COPY "${CAPNP_OUT_DIR}/driver.capnp")
set(CAPNP_GEN_HDR "${CAPNP_OUT_DIR}/driver.capnp.h")
set(CAPNP_GEN_SRC "${CAPNP_OUT_DIR}/driver.capnp.c++")

# Resolve capnp compiler path
set(CAPNP_COMPILER capnp)
if (TARGET CapnProto::capnp_tool)
    set(CAPNP_COMPILER $<TARGET_FILE:CapnProto::capnp_tool>)
elseif (DEFINED CAPNP_EXECUTABLE)
    set(CAPNP_COMPILER "${CAPNP_EXECUTABLE}")
endif()

# Set up the capnp compile command with PATH modification on Linux
if(UNIX AND NOT APPLE)
    if (TARGET CapnProto::capnp_tool)
        set(CAPNP_COMPILE_COMMAND ${CMAKE_COMMAND} -E env "PATH=$<TARGET_FILE_DIR:CapnProto::capnp_tool>:$ENV{PATH}" ${CAPNP_COMPILER} compile -o c++:. "driver.capnp")
    elseif (DEFINED CAPNP_EXECUTABLE)
        get_filename_component(CAPNP_DIR "${CAPNP_EXECUTABLE}" DIRECTORY)
        set(CAPNP_COMPILE_COMMAND ${CMAKE_COMMAND} -E env "PATH=${CAPNP_DIR}:$ENV{PATH}" ${CAPNP_COMPILER} compile -o c++:. "driver.capnp")
    else()
        set(CAPNP_COMPILE_COMMAND ${CAPNP_COMPILER} compile -o c++:. "driver.capnp")
    endif()
else()
    set(CAPNP_COMPILE_COMMAND ${CAPNP_COMPILER} compile -o c++:. "driver.capnp")
endif()

add_custom_command(
    OUTPUT "${CAPNP_GEN_HDR}" "${CAPNP_GEN_SRC}"
    COMMAND ${CMAKE_COMMAND} -E make_directory "${CAPNP_OUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${CAPNP_SCHEMA}" "${CAPNP_SCHEMA_COPY}"
    COMMAND ${CAPNP_COMPILE_COMMAND}
    DEPENDS "${CAPNP_SCHEMA}"
    WORKING_DIRECTORY "${CAPNP_OUT_DIR}"
    COMMENT "Generating Cap'n Proto C++ from driver.capnp"
    VERBATIM
)

# Group generated files for IDEs
source_group(TREE "${CAPNP_OUT_DIR}" PREFIX "Generated Files" FILES "${CAPNP_GEN_SRC}" "${CAPNP_GEN_HDR}")
# --------------------------------------------------------------

# ----------------------- OpenVR (vendor) ----------------------
set(OPENVR_ROOT "${CMAKE_CURRENT_LIST_DIR}/../vendor/openvr")
set(OPENVR_INCLUDE_DIR "${OPENVR_ROOT}/headers")

if (WIN32)
    link_directories("${OPENVR_ROOT}/lib/win64")
    link_directories("${OPENVR_ROOT}/bin/win64")
elseif(UNIX AND NOT APPLE)
    link_directories("${OPENVR_ROOT}/lib/linux64")
endif()
# --------------------------------------------------------------

# -----------------------Main-----------------------------------
add_library(${DRIVER_TARGET_NAME} SHARED ${driver_HDR} ${driver_SRC} ${driver_IDL})
target_include_directories(${DRIVER_TARGET_NAME} PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/include")

set_property(TARGET ${DRIVER_TARGET_NAME} PROPERTY CXX_STANDARD 20)
set_property(TARGET ${DRIVER_TARGET_NAME} PROPERTY CXX_STANDARD_REQUIRED ON)
# --------------------------------------------------------------

# ----------------------- Cap'n Proto --------------------------
# Link libraries
target_link_libraries(${DRIVER_TARGET_NAME} PRIVATE CapnProto::capnp CapnProto::capnp-rpc CapnProto::kj CapnProto::kj-async)

# Add generated sources to the target and include directory
add_custom_target(capnp_generated ALL DEPENDS "${CAPNP_GEN_HDR}" "${CAPNP_GEN_SRC}")
add_dependencies(${DRIVER_TARGET_NAME} capnp_generated)

target_sources(${DRIVER_TARGET_NAME} PRIVATE "${CAPNP_GEN_SRC}" "${CAPNP_GEN_HDR}")
target_include_directories(${DRIVER_TARGET_NAME} PRIVATE "${CAPNP_OUT_DIR}")
# --------------------------------------------------------------

# ----------------------- OpenVR (vendor) ----------------------
target_link_libraries(${DRIVER_TARGET_NAME} PRIVATE openvr_api)
target_include_directories(${DRIVER_TARGET_NAME} PRIVATE "${OPENVR_INCLUDE_DIR}")

# Link MinHook after target exists
if (WIN32 AND USE_HOOKS)
    target_link_libraries(${DRIVER_TARGET_NAME} PRIVATE ${MINHOOK_LIB})
endif()

if (UNIX AND NOT APPLE)
    set_target_properties(${DRIVER_TARGET_NAME} PROPERTIES
        BUILD_RPATH "$ORIGIN"
        INSTALL_RPATH "$ORIGIN")
endif()
# --------------------------------------------------------------

# ----------------------- Packaging (Pack/00Amethyst) ---------
# Update pack root based on target name
if(USE_HOOKS)
    set(PACK_ROOT "${CMAKE_CURRENT_BINARY_DIR}/Pack/00Amethyst")
else()
    set(PACK_ROOT "${CMAKE_CURRENT_BINARY_DIR}/Pack/Amethyst")
endif()

if (WIN32)
    set(PACK_BIN_DIR "${PACK_ROOT}/bin/win64")
elseif(UNIX AND NOT APPLE)
    set(PACK_BIN_DIR "${PACK_ROOT}/bin/linux64")
else()
    set(PACK_BIN_DIR "${PACK_ROOT}/bin")
endif()

if(USE_HOOKS)
    set(MANIFEST_SRC "${CMAKE_CURRENT_LIST_DIR}/../external/00driver_resources/driver.vrdrivermanifest")
else()
    set(MANIFEST_SRC "${CMAKE_CURRENT_LIST_DIR}/../external/driver_resources/driver.vrdrivermanifest")
endif()

add_custom_command(TARGET ${DRIVER_TARGET_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${PACK_BIN_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${MANIFEST_SRC}" "${PACK_ROOT}/driver.vrdrivermanifest"
    COMMENT "Packing driver files to ${PACK_ROOT}"
    VERBATIM
)

if(UNIX AND NOT APPLE)
    add_custom_command(TARGET ${DRIVER_TARGET_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different 
            "$<TARGET_FILE:${DRIVER_TARGET_NAME}>" 
            "${PACK_BIN_DIR}/$<TARGET_FILE_BASE_NAME:${DRIVER_TARGET_NAME}>$<TARGET_FILE_SUFFIX:${DRIVER_TARGET_NAME}>"
        COMMENT "Copying driver library without lib prefix"
        VERBATIM
    )
else()
    add_custom_command(TARGET ${DRIVER_TARGET_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:${DRIVER_TARGET_NAME}>" "${PACK_BIN_DIR}"
        COMMENT "Copying driver library"
        VERBATIM
    )
endif()
# --------------------------------------------------------------

