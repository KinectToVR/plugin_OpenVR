@0xd7e9a6c1a9a01e2f;

using Cxx = import "/capnp/c++.capnp";
$Cxx.namespace("amethyst::driver");

# - HRESULT is not modeled; return only the out params (if any).
# - Definitions, structs, and enums mirror MIDL names and fields.

# ---------------- Data contracts (from DataContract.idl) ----------------

enum TrackerType {
  trackerHanded      @0;
  trackerLeftFoot    @1;
  trackerRightFoot   @2;
  trackerLeftShoulder@3;
  trackerRightShoulder@4;
  trackerLeftElbow   @5;
  trackerRightElbow  @6;
  trackerLeftKnee    @7;
  trackerRightKnee   @8;
  trackerWaist       @9;
  trackerChest       @10;
  trackerCamera      @11;
  trackerKeyboard    @12;
  trackerHead        @13;
  trackerLeftHand    @14;
  trackerRightHand   @15;
}

struct Vector3 {
  x @0 :Float32;
  y @1 :Float32;
  z @2 :Float32;
}

struct Quaternion {
  x @0 :Float32;
  y @1 :Float32;
  z @2 :Float32;
  w @3 :Float32;
}

# Mirrors the nullable wrapper used in the IDL.
struct Vector3Nullable {
  hasValue @0 :Bool;
  value    @1 :Vector3;
}

struct TrackerBase {
  connectionState   @0 :Bool;
  trackingState     @1 :Bool;
  serial            @2 :Text;       # [string] char* Serial

  role              @3 :TrackerType;
  position          @4 :Vector3;
  orientation       @5 :Quaternion;

  velocity          @6 :Vector3Nullable;
  acceleration      @7 :Vector3Nullable;
  angularVelocity   @8 :Vector3Nullable;
  angularAcceleration @9 :Vector3Nullable;
}

struct DriverPose {
  connectionState @0 :Bool;
  trackingState   @1 :Bool;

  position        @2 :Vector3;
  orientation     @3 :Quaternion;
}

# ---------------- Service contracts (from IVersionedApi.idl, IDriverService.idl) ----------------


interface IDriverService {
  getVersion @0 () -> (apiVersion :UInt32);

  setTrackerState   @1 (tracker :TrackerBase) -> ();
  updateTracker     @2 (tracker :TrackerBase) -> ();

  requestVrRestart  @3 (message :Text) -> ();
  pingDriverService @4 () -> (ms :Int64);

  setDriverPose     @5 (id :UInt32, pose :DriverPose) -> ();
  enableOverride    @6 (id :UInt32, isEnabled :Bool) -> ();

  updateInputBoolean @7 (tracker :TrackerType, path :Text, value :Bool) -> ();
  updateInputScalar  @8 (tracker :TrackerType, path :Text, value :Float32) -> ();
}
