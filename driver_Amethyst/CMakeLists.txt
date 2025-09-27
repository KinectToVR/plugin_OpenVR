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

add_library(${DRIVER_TARGET_NAME} SHARED ${driver_HDR} ${driver_SRC} ${driver_IDL})
target_include_directories(${DRIVER_TARGET_NAME} PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/include")

if (CMAKE_VERSION VERSION_GREATER 3.12)
  set_property(TARGET ${DRIVER_TARGET_NAME} PROPERTY CXX_STANDARD 20)
endif()

# ----------------------- MinHook support ----------------------
if (WIN32 AND USE_HOOKS)
    find_package(minhook CONFIG REQUIRED)
    target_link_libraries(${DRIVER_TARGET_NAME} PRIVATE minhook::minhook)
endif()
# --------------------------------------------------------------

# ----------------------- Cap'n Proto --------------------------
find_package(CapnProto CONFIG REQUIRED)

# Link libraries
target_link_libraries(${DRIVER_TARGET_NAME} PRIVATE CapnProto::capnp CapnProto::capnp-rpc CapnProto::kj CapnProto::kj-async)

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

add_custom_command(
    OUTPUT "${CAPNP_GEN_HDR}" "${CAPNP_GEN_SRC}"
    COMMAND ${CMAKE_COMMAND} -E make_directory "${CAPNP_OUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${CAPNP_SCHEMA}" "${CAPNP_SCHEMA_COPY}"
    COMMAND ${CAPNP_COMPILER} compile -o c++:. "driver.capnp"
    DEPENDS "${CAPNP_SCHEMA}"
    WORKING_DIRECTORY "${CAPNP_OUT_DIR}"
    COMMENT "Generating Cap'n Proto C++ from driver.capnp"
    VERBATIM
)

# Add generated sources to the target and include directory
add_custom_target(capnp_generated ALL DEPENDS "${CAPNP_GEN_HDR}" "${CAPNP_GEN_SRC}")
add_dependencies(${DRIVER_TARGET_NAME} capnp_generated)

target_sources(${DRIVER_TARGET_NAME} PRIVATE "${CAPNP_GEN_SRC}" "${CAPNP_GEN_HDR}")
target_include_directories(${DRIVER_TARGET_NAME} PRIVATE "${CAPNP_OUT_DIR}")

# Group generated files for IDEs
source_group(TREE "${CAPNP_OUT_DIR}" PREFIX "Generated Files" FILES "${CAPNP_GEN_SRC}" "${CAPNP_GEN_HDR}")
# --------------------------------------------------------------

# ----------------------- OpenVR (vendor) ----------------------
set(OPENVR_ROOT "${CMAKE_CURRENT_LIST_DIR}/../vendor/openvr")
set(OPENVR_INCLUDE_DIR "${OPENVR_ROOT}/headers")

if (WIN32)
    if (CMAKE_SIZEOF_VOID_P EQUAL 8)
        set(OPENVR_IMPLIB   "${OPENVR_ROOT}/lib/win64/openvr_api.lib")
        set(OPENVR_RUNTIME  "${OPENVR_ROOT}/bin/win64/openvr_api.dll")
    else()
        set(OPENVR_IMPLIB   "${OPENVR_ROOT}/lib/win32/openvr_api.lib")
        set(OPENVR_RUNTIME  "${OPENVR_ROOT}/bin/win32/openvr_api.dll")
    endif()
    add_library(openvr::openvr SHARED IMPORTED)
    set_target_properties(openvr::openvr PROPERTIES
        IMPORTED_IMPLIB "${OPENVR_IMPLIB}"
        IMPORTED_LOCATION "${OPENVR_RUNTIME}"
        INTERFACE_INCLUDE_DIRECTORIES "${OPENVR_INCLUDE_DIR}"
    )
elseif(UNIX AND NOT APPLE)
    if (CMAKE_SIZEOF_VOID_P EQUAL 8)
        set(OPENVR_RUNTIME "${OPENVR_ROOT}/lib/linux64/libopenvr_api.so")
    else()
        set(OPENVR_RUNTIME "${OPENVR_ROOT}/lib/linux32/libopenvr_api.so")
    endif()
    add_library(openvr::openvr SHARED IMPORTED)
    set_target_properties(openvr::openvr PROPERTIES
        IMPORTED_LOCATION "${OPENVR_RUNTIME}"
        INTERFACE_INCLUDE_DIRECTORIES "${OPENVR_INCLUDE_DIR}"
    )
endif()

if (TARGET openvr::openvr)
    target_link_libraries(${DRIVER_TARGET_NAME} PRIVATE openvr::openvr)
    target_include_directories(${DRIVER_TARGET_NAME} PRIVATE "${OPENVR_INCLUDE_DIR}")

    add_custom_command(TARGET ${DRIVER_TARGET_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "${OPENVR_RUNTIME}" "$<TARGET_FILE_DIR:${DRIVER_TARGET_NAME}>"
        VERBATIM)

    if (UNIX AND NOT APPLE)
        set_target_properties(${DRIVER_TARGET_NAME} PROPERTIES
            BUILD_RPATH "$ORIGIN"
            INSTALL_RPATH "$ORIGIN")
    endif()
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
    if (CMAKE_SIZEOF_VOID_P EQUAL 8)
        set(PACK_BIN_DIR "${PACK_ROOT}/bin/win64")
    else()
        set(PACK_BIN_DIR "${PACK_ROOT}/bin/win32")
    endif()
else()
    # Default bin directory if non-Windows
    set(PACK_BIN_DIR "${PACK_ROOT}/bin")
endif()

if(USE_HOOKS)
    set(MANIFEST_SRC "${CMAKE_CURRENT_LIST_DIR}/../external/00driver_resources/driver.vrdrivermanifest")
else()
    set(MANIFEST_SRC "${CMAKE_CURRENT_LIST_DIR}/../external/driver_resources/driver.vrdrivermanifest")
endif()

add_custom_command(TARGET ${DRIVER_TARGET_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${PACK_BIN_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:${DRIVER_TARGET_NAME}>" "${PACK_BIN_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${MANIFEST_SRC}" "${PACK_ROOT}/driver.vrdrivermanifest"
    COMMENT "Packing driver files to ${PACK_ROOT}"
    VERBATIM
)
# --------------------------------------------------------------
