using Capnp;
using Capnp.Rpc;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Amethyst.Driver
{
    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0x8e3605de201b09a9UL)]
    public enum TrackerType : ushort
    {
        trackerHanded,
        trackerLeftFoot,
        trackerRightFoot,
        trackerLeftShoulder,
        trackerRightShoulder,
        trackerLeftElbow,
        trackerRightElbow,
        trackerLeftKnee,
        trackerRightKnee,
        trackerWaist,
        trackerChest,
        trackerCamera,
        trackerKeyboard,
        trackerHead,
        trackerLeftHand,
        trackerRightHand
    }

    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xee9ad9969a94d62aUL)]
    public class Vector3 : ICapnpSerializable
    {
        public const UInt64 typeId = 0xee9ad9969a94d62aUL;
        void ICapnpSerializable.Deserialize(DeserializerState arg_)
        {
            var reader = READER.create(arg_);
            X = reader.X;
            Y = reader.Y;
            Z = reader.Z;
            applyDefaults();
        }

        public void serialize(WRITER writer)
        {
            writer.X = X;
            writer.Y = Y;
            writer.Z = Z;
        }

        void ICapnpSerializable.Serialize(SerializerState arg_)
        {
            serialize(arg_.Rewrap<WRITER>());
        }

        public void applyDefaults()
        {
        }

        public float X
        {
            get;
            set;
        }

        public float Y
        {
            get;
            set;
        }

        public float Z
        {
            get;
            set;
        }

        public struct READER
        {
            readonly DeserializerState ctx;
            public READER(DeserializerState ctx)
            {
                this.ctx = ctx;
            }

            public static READER create(DeserializerState ctx) => new READER(ctx);
            public static implicit operator DeserializerState(READER reader) => reader.ctx;
            public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            public float X => ctx.ReadDataFloat(0UL, 0F);
            public float Y => ctx.ReadDataFloat(32UL, 0F);
            public float Z => ctx.ReadDataFloat(64UL, 0F);
        }

        public class WRITER : SerializerState
        {
            public WRITER()
            {
                this.SetStruct(2, 0);
            }

            public float X
            {
                get => this.ReadDataFloat(0UL, 0F);
                set => this.WriteData(0UL, value, 0F);
            }

            public float Y
            {
                get => this.ReadDataFloat(32UL, 0F);
                set => this.WriteData(32UL, value, 0F);
            }

            public float Z
            {
                get => this.ReadDataFloat(64UL, 0F);
                set => this.WriteData(64UL, value, 0F);
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xe171834850fa1865UL)]
    public class Quaternion : ICapnpSerializable
    {
        public const UInt64 typeId = 0xe171834850fa1865UL;
        void ICapnpSerializable.Deserialize(DeserializerState arg_)
        {
            var reader = READER.create(arg_);
            X = reader.X;
            Y = reader.Y;
            Z = reader.Z;
            W = reader.W;
            applyDefaults();
        }

        public void serialize(WRITER writer)
        {
            writer.X = X;
            writer.Y = Y;
            writer.Z = Z;
            writer.W = W;
        }

        void ICapnpSerializable.Serialize(SerializerState arg_)
        {
            serialize(arg_.Rewrap<WRITER>());
        }

        public void applyDefaults()
        {
        }

        public float X
        {
            get;
            set;
        }

        public float Y
        {
            get;
            set;
        }

        public float Z
        {
            get;
            set;
        }

        public float W
        {
            get;
            set;
        }

        public struct READER
        {
            readonly DeserializerState ctx;
            public READER(DeserializerState ctx)
            {
                this.ctx = ctx;
            }

            public static READER create(DeserializerState ctx) => new READER(ctx);
            public static implicit operator DeserializerState(READER reader) => reader.ctx;
            public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            public float X => ctx.ReadDataFloat(0UL, 0F);
            public float Y => ctx.ReadDataFloat(32UL, 0F);
            public float Z => ctx.ReadDataFloat(64UL, 0F);
            public float W => ctx.ReadDataFloat(96UL, 0F);
        }

        public class WRITER : SerializerState
        {
            public WRITER()
            {
                this.SetStruct(2, 0);
            }

            public float X
            {
                get => this.ReadDataFloat(0UL, 0F);
                set => this.WriteData(0UL, value, 0F);
            }

            public float Y
            {
                get => this.ReadDataFloat(32UL, 0F);
                set => this.WriteData(32UL, value, 0F);
            }

            public float Z
            {
                get => this.ReadDataFloat(64UL, 0F);
                set => this.WriteData(64UL, value, 0F);
            }

            public float W
            {
                get => this.ReadDataFloat(96UL, 0F);
                set => this.WriteData(96UL, value, 0F);
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xfd15beff506e2015UL)]
    public class Vector3Nullable : ICapnpSerializable
    {
        public const UInt64 typeId = 0xfd15beff506e2015UL;
        void ICapnpSerializable.Deserialize(DeserializerState arg_)
        {
            var reader = READER.create(arg_);
            HasValue = reader.HasValue;
            Value = CapnpSerializable.Create<Amethyst.Driver.Vector3>(reader.Value);
            applyDefaults();
        }

        public void serialize(WRITER writer)
        {
            writer.HasValue = HasValue;
            Value?.serialize(writer.Value);
        }

        void ICapnpSerializable.Serialize(SerializerState arg_)
        {
            serialize(arg_.Rewrap<WRITER>());
        }

        public void applyDefaults()
        {
        }

        public bool HasValue
        {
            get;
            set;
        }

        public Amethyst.Driver.Vector3 Value
        {
            get;
            set;
        }

        public struct READER
        {
            readonly DeserializerState ctx;
            public READER(DeserializerState ctx)
            {
                this.ctx = ctx;
            }

            public static READER create(DeserializerState ctx) => new READER(ctx);
            public static implicit operator DeserializerState(READER reader) => reader.ctx;
            public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            public bool HasValue => ctx.ReadDataBool(0UL, false);
            public Amethyst.Driver.Vector3.READER Value => ctx.ReadStruct(0, Amethyst.Driver.Vector3.READER.create);
        }

        public class WRITER : SerializerState
        {
            public WRITER()
            {
                this.SetStruct(1, 1);
            }

            public bool HasValue
            {
                get => this.ReadDataBool(0UL, false);
                set => this.WriteData(0UL, value, false);
            }

            public Amethyst.Driver.Vector3.WRITER Value
            {
                get => BuildPointer<Amethyst.Driver.Vector3.WRITER>(0);
                set => Link(0, value);
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xea18f58d14fff4ebUL)]
    public class TrackerBase : ICapnpSerializable
    {
        public const UInt64 typeId = 0xea18f58d14fff4ebUL;
        void ICapnpSerializable.Deserialize(DeserializerState arg_)
        {
            var reader = READER.create(arg_);
            ConnectionState = reader.ConnectionState;
            TrackingState = reader.TrackingState;
            Serial = reader.Serial;
            Role = reader.Role;
            Position = CapnpSerializable.Create<Amethyst.Driver.Vector3>(reader.Position);
            Orientation = CapnpSerializable.Create<Amethyst.Driver.Quaternion>(reader.Orientation);
            Velocity = CapnpSerializable.Create<Amethyst.Driver.Vector3Nullable>(reader.Velocity);
            Acceleration = CapnpSerializable.Create<Amethyst.Driver.Vector3Nullable>(reader.Acceleration);
            AngularVelocity = CapnpSerializable.Create<Amethyst.Driver.Vector3Nullable>(reader.AngularVelocity);
            AngularAcceleration = CapnpSerializable.Create<Amethyst.Driver.Vector3Nullable>(reader.AngularAcceleration);
            applyDefaults();
        }

        public void serialize(WRITER writer)
        {
            writer.ConnectionState = ConnectionState;
            writer.TrackingState = TrackingState;
            writer.Serial = Serial;
            writer.Role = Role;
            Position?.serialize(writer.Position);
            Orientation?.serialize(writer.Orientation);
            Velocity?.serialize(writer.Velocity);
            Acceleration?.serialize(writer.Acceleration);
            AngularVelocity?.serialize(writer.AngularVelocity);
            AngularAcceleration?.serialize(writer.AngularAcceleration);
        }

        void ICapnpSerializable.Serialize(SerializerState arg_)
        {
            serialize(arg_.Rewrap<WRITER>());
        }

        public void applyDefaults()
        {
        }

        public bool ConnectionState
        {
            get;
            set;
        }

        public bool TrackingState
        {
            get;
            set;
        }

        public string Serial
        {
            get;
            set;
        }

        public Amethyst.Driver.TrackerType Role
        {
            get;
            set;
        }

        public Amethyst.Driver.Vector3 Position
        {
            get;
            set;
        }

        public Amethyst.Driver.Quaternion Orientation
        {
            get;
            set;
        }

        public Amethyst.Driver.Vector3Nullable Velocity
        {
            get;
            set;
        }

        public Amethyst.Driver.Vector3Nullable Acceleration
        {
            get;
            set;
        }

        public Amethyst.Driver.Vector3Nullable AngularVelocity
        {
            get;
            set;
        }

        public Amethyst.Driver.Vector3Nullable AngularAcceleration
        {
            get;
            set;
        }

        public struct READER
        {
            readonly DeserializerState ctx;
            public READER(DeserializerState ctx)
            {
                this.ctx = ctx;
            }

            public static READER create(DeserializerState ctx) => new READER(ctx);
            public static implicit operator DeserializerState(READER reader) => reader.ctx;
            public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            public bool ConnectionState => ctx.ReadDataBool(0UL, false);
            public bool TrackingState => ctx.ReadDataBool(1UL, false);
            public string Serial => ctx.ReadText(0, null);
            public Amethyst.Driver.TrackerType Role => (Amethyst.Driver.TrackerType)ctx.ReadDataUShort(16UL, (ushort)0);
            public Amethyst.Driver.Vector3.READER Position => ctx.ReadStruct(1, Amethyst.Driver.Vector3.READER.create);
            public Amethyst.Driver.Quaternion.READER Orientation => ctx.ReadStruct(2, Amethyst.Driver.Quaternion.READER.create);
            public Amethyst.Driver.Vector3Nullable.READER Velocity => ctx.ReadStruct(3, Amethyst.Driver.Vector3Nullable.READER.create);
            public Amethyst.Driver.Vector3Nullable.READER Acceleration => ctx.ReadStruct(4, Amethyst.Driver.Vector3Nullable.READER.create);
            public Amethyst.Driver.Vector3Nullable.READER AngularVelocity => ctx.ReadStruct(5, Amethyst.Driver.Vector3Nullable.READER.create);
            public Amethyst.Driver.Vector3Nullable.READER AngularAcceleration => ctx.ReadStruct(6, Amethyst.Driver.Vector3Nullable.READER.create);
        }

        public class WRITER : SerializerState
        {
            public WRITER()
            {
                this.SetStruct(1, 7);
            }

            public bool ConnectionState
            {
                get => this.ReadDataBool(0UL, false);
                set => this.WriteData(0UL, value, false);
            }

            public bool TrackingState
            {
                get => this.ReadDataBool(1UL, false);
                set => this.WriteData(1UL, value, false);
            }

            public string Serial
            {
                get => this.ReadText(0, null);
                set => this.WriteText(0, value, null);
            }

            public Amethyst.Driver.TrackerType Role
            {
                get => (Amethyst.Driver.TrackerType)this.ReadDataUShort(16UL, (ushort)0);
                set => this.WriteData(16UL, (ushort)value, (ushort)0);
            }

            public Amethyst.Driver.Vector3.WRITER Position
            {
                get => BuildPointer<Amethyst.Driver.Vector3.WRITER>(1);
                set => Link(1, value);
            }

            public Amethyst.Driver.Quaternion.WRITER Orientation
            {
                get => BuildPointer<Amethyst.Driver.Quaternion.WRITER>(2);
                set => Link(2, value);
            }

            public Amethyst.Driver.Vector3Nullable.WRITER Velocity
            {
                get => BuildPointer<Amethyst.Driver.Vector3Nullable.WRITER>(3);
                set => Link(3, value);
            }

            public Amethyst.Driver.Vector3Nullable.WRITER Acceleration
            {
                get => BuildPointer<Amethyst.Driver.Vector3Nullable.WRITER>(4);
                set => Link(4, value);
            }

            public Amethyst.Driver.Vector3Nullable.WRITER AngularVelocity
            {
                get => BuildPointer<Amethyst.Driver.Vector3Nullable.WRITER>(5);
                set => Link(5, value);
            }

            public Amethyst.Driver.Vector3Nullable.WRITER AngularAcceleration
            {
                get => BuildPointer<Amethyst.Driver.Vector3Nullable.WRITER>(6);
                set => Link(6, value);
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xe29c29b448f2a51aUL)]
    public class DriverPose : ICapnpSerializable
    {
        public const UInt64 typeId = 0xe29c29b448f2a51aUL;
        void ICapnpSerializable.Deserialize(DeserializerState arg_)
        {
            var reader = READER.create(arg_);
            ConnectionState = reader.ConnectionState;
            TrackingState = reader.TrackingState;
            Position = CapnpSerializable.Create<Amethyst.Driver.Vector3>(reader.Position);
            Orientation = CapnpSerializable.Create<Amethyst.Driver.Quaternion>(reader.Orientation);
            applyDefaults();
        }

        public void serialize(WRITER writer)
        {
            writer.ConnectionState = ConnectionState;
            writer.TrackingState = TrackingState;
            Position?.serialize(writer.Position);
            Orientation?.serialize(writer.Orientation);
        }

        void ICapnpSerializable.Serialize(SerializerState arg_)
        {
            serialize(arg_.Rewrap<WRITER>());
        }

        public void applyDefaults()
        {
        }

        public bool ConnectionState
        {
            get;
            set;
        }

        public bool TrackingState
        {
            get;
            set;
        }

        public Amethyst.Driver.Vector3 Position
        {
            get;
            set;
        }

        public Amethyst.Driver.Quaternion Orientation
        {
            get;
            set;
        }

        public struct READER
        {
            readonly DeserializerState ctx;
            public READER(DeserializerState ctx)
            {
                this.ctx = ctx;
            }

            public static READER create(DeserializerState ctx) => new READER(ctx);
            public static implicit operator DeserializerState(READER reader) => reader.ctx;
            public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            public bool ConnectionState => ctx.ReadDataBool(0UL, false);
            public bool TrackingState => ctx.ReadDataBool(1UL, false);
            public Amethyst.Driver.Vector3.READER Position => ctx.ReadStruct(0, Amethyst.Driver.Vector3.READER.create);
            public Amethyst.Driver.Quaternion.READER Orientation => ctx.ReadStruct(1, Amethyst.Driver.Quaternion.READER.create);
        }

        public class WRITER : SerializerState
        {
            public WRITER()
            {
                this.SetStruct(1, 2);
            }

            public bool ConnectionState
            {
                get => this.ReadDataBool(0UL, false);
                set => this.WriteData(0UL, value, false);
            }

            public bool TrackingState
            {
                get => this.ReadDataBool(1UL, false);
                set => this.WriteData(1UL, value, false);
            }

            public Amethyst.Driver.Vector3.WRITER Position
            {
                get => BuildPointer<Amethyst.Driver.Vector3.WRITER>(0);
                set => Link(0, value);
            }

            public Amethyst.Driver.Quaternion.WRITER Orientation
            {
                get => BuildPointer<Amethyst.Driver.Quaternion.WRITER>(1);
                set => Link(1, value);
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xf4da585696335cf1UL), Proxy(typeof(IDriverService_Proxy)), Skeleton(typeof(IDriverService_Skeleton))]
    public interface IIDriverService : IDisposable
    {
        Task<uint> GetVersion(CancellationToken cancellationToken_ = default);
        Task SetTrackerState(Amethyst.Driver.TrackerBase tracker, CancellationToken cancellationToken_ = default);
        Task UpdateTracker(Amethyst.Driver.TrackerBase tracker, CancellationToken cancellationToken_ = default);
        Task RequestVrRestart(string message, CancellationToken cancellationToken_ = default);
        Task<long> PingDriverService(CancellationToken cancellationToken_ = default);
        Task SetDriverPose(uint id, Amethyst.Driver.DriverPose pose, CancellationToken cancellationToken_ = default);
        Task EnableOverride(uint id, bool isEnabled, CancellationToken cancellationToken_ = default);
        Task UpdateInputBoolean(Amethyst.Driver.TrackerType tracker, string path, bool value, CancellationToken cancellationToken_ = default);
        Task UpdateInputScalar(Amethyst.Driver.TrackerType tracker, string path, float value, CancellationToken cancellationToken_ = default);
    }

    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xf4da585696335cf1UL)]
    public class IDriverService_Proxy : Proxy, IIDriverService
    {
        public async Task<uint> GetVersion(CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_GetVersion.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_GetVersion()
            {};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 0, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_GetVersion>(d_);
                return (r_.ApiVersion);
            }
        }

        public async Task SetTrackerState(Amethyst.Driver.TrackerBase tracker, CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_SetTrackerState.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_SetTrackerState()
            {Tracker = tracker};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 1, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_SetTrackerState>(d_);
                return;
            }
        }

        public async Task UpdateTracker(Amethyst.Driver.TrackerBase tracker, CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_UpdateTracker.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_UpdateTracker()
            {Tracker = tracker};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 2, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_UpdateTracker>(d_);
                return;
            }
        }

        public async Task RequestVrRestart(string message, CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_RequestVrRestart.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_RequestVrRestart()
            {Message = message};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 3, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_RequestVrRestart>(d_);
                return;
            }
        }

        public async Task<long> PingDriverService(CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_PingDriverService.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_PingDriverService()
            {};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 4, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_PingDriverService>(d_);
                return (r_.Ms);
            }
        }

        public async Task SetDriverPose(uint id, Amethyst.Driver.DriverPose pose, CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_SetDriverPose.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_SetDriverPose()
            {Id = id, Pose = pose};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 5, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_SetDriverPose>(d_);
                return;
            }
        }

        public async Task EnableOverride(uint id, bool isEnabled, CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_EnableOverride.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_EnableOverride()
            {Id = id, IsEnabled = isEnabled};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 6, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_EnableOverride>(d_);
                return;
            }
        }

        public async Task UpdateInputBoolean(Amethyst.Driver.TrackerType tracker, string path, bool value, CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_UpdateInputBoolean.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_UpdateInputBoolean()
            {Tracker = tracker, Path = path, Value = value};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 7, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_UpdateInputBoolean>(d_);
                return;
            }
        }

        public async Task UpdateInputScalar(Amethyst.Driver.TrackerType tracker, string path, float value, CancellationToken cancellationToken_ = default)
        {
            var in_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Params_UpdateInputScalar.WRITER>();
            var arg_ = new Amethyst.Driver.IDriverService.Params_UpdateInputScalar()
            {Tracker = tracker, Path = path, Value = value};
            arg_?.serialize(in_);
            using (var d_ = await Call(17643511619087719665UL, 8, in_.Rewrap<DynamicSerializerState>(), false, cancellationToken_).WhenReturned)
            {
                var r_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Result_UpdateInputScalar>(d_);
                return;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xf4da585696335cf1UL)]
    public class IDriverService_Skeleton : Skeleton<IIDriverService>
    {
        public IDriverService_Skeleton()
        {
            SetMethodTable(GetVersion, SetTrackerState, UpdateTracker, RequestVrRestart, PingDriverService, SetDriverPose, EnableOverride, UpdateInputBoolean, UpdateInputScalar);
        }

        public override ulong InterfaceId => 17643511619087719665UL;
        Task<AnswerOrCounterquestion> GetVersion(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                return Impatient.MaybeTailCall(Impl.GetVersion(cancellationToken_), apiVersion =>
                {
                    var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_GetVersion.WRITER>();
                    var r_ = new Amethyst.Driver.IDriverService.Result_GetVersion{ApiVersion = apiVersion};
                    r_.serialize(s_);
                    return s_;
                }

                );
            }
        }

        async Task<AnswerOrCounterquestion> SetTrackerState(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                var in_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Params_SetTrackerState>(d_);
                await Impl.SetTrackerState(in_.Tracker, cancellationToken_);
                var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_SetTrackerState.WRITER>();
                return s_;
            }
        }

        async Task<AnswerOrCounterquestion> UpdateTracker(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                var in_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Params_UpdateTracker>(d_);
                await Impl.UpdateTracker(in_.Tracker, cancellationToken_);
                var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_UpdateTracker.WRITER>();
                return s_;
            }
        }

        async Task<AnswerOrCounterquestion> RequestVrRestart(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                var in_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Params_RequestVrRestart>(d_);
                await Impl.RequestVrRestart(in_.Message, cancellationToken_);
                var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_RequestVrRestart.WRITER>();
                return s_;
            }
        }

        Task<AnswerOrCounterquestion> PingDriverService(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                return Impatient.MaybeTailCall(Impl.PingDriverService(cancellationToken_), ms =>
                {
                    var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_PingDriverService.WRITER>();
                    var r_ = new Amethyst.Driver.IDriverService.Result_PingDriverService{Ms = ms};
                    r_.serialize(s_);
                    return s_;
                }

                );
            }
        }

        async Task<AnswerOrCounterquestion> SetDriverPose(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                var in_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Params_SetDriverPose>(d_);
                await Impl.SetDriverPose(in_.Id, in_.Pose, cancellationToken_);
                var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_SetDriverPose.WRITER>();
                return s_;
            }
        }

        async Task<AnswerOrCounterquestion> EnableOverride(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                var in_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Params_EnableOverride>(d_);
                await Impl.EnableOverride(in_.Id, in_.IsEnabled, cancellationToken_);
                var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_EnableOverride.WRITER>();
                return s_;
            }
        }

        async Task<AnswerOrCounterquestion> UpdateInputBoolean(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                var in_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Params_UpdateInputBoolean>(d_);
                await Impl.UpdateInputBoolean(in_.Tracker, in_.Path, in_.Value, cancellationToken_);
                var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_UpdateInputBoolean.WRITER>();
                return s_;
            }
        }

        async Task<AnswerOrCounterquestion> UpdateInputScalar(DeserializerState d_, CancellationToken cancellationToken_)
        {
            using (d_)
            {
                var in_ = CapnpSerializable.Create<Amethyst.Driver.IDriverService.Params_UpdateInputScalar>(d_);
                await Impl.UpdateInputScalar(in_.Tracker, in_.Path, in_.Value, cancellationToken_);
                var s_ = SerializerState.CreateForRpc<Amethyst.Driver.IDriverService.Result_UpdateInputScalar.WRITER>();
                return s_;
            }
        }
    }

    public static class IDriverService
    {
        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xf8708b1fc51f1d74UL)]
        public class Params_GetVersion : ICapnpSerializable
        {
            public const UInt64 typeId = 0xf8708b1fc51f1d74UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xc51d6fc0c71c448bUL)]
        public class Result_GetVersion : ICapnpSerializable
        {
            public const UInt64 typeId = 0xc51d6fc0c71c448bUL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                ApiVersion = reader.ApiVersion;
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                writer.ApiVersion = ApiVersion;
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public uint ApiVersion
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public uint ApiVersion => ctx.ReadDataUInt(0UL, 0U);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(1, 0);
                }

                public uint ApiVersion
                {
                    get => this.ReadDataUInt(0UL, 0U);
                    set => this.WriteData(0UL, value, 0U);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xc94be266b8d11466UL)]
        public class Params_SetTrackerState : ICapnpSerializable
        {
            public const UInt64 typeId = 0xc94be266b8d11466UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                Tracker = CapnpSerializable.Create<Amethyst.Driver.TrackerBase>(reader.Tracker);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                Tracker?.serialize(writer.Tracker);
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public Amethyst.Driver.TrackerBase Tracker
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public Amethyst.Driver.TrackerBase.READER Tracker => ctx.ReadStruct(0, Amethyst.Driver.TrackerBase.READER.create);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 1);
                }

                public Amethyst.Driver.TrackerBase.WRITER Tracker
                {
                    get => BuildPointer<Amethyst.Driver.TrackerBase.WRITER>(0);
                    set => Link(0, value);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xa6e2e803d7dd6d34UL)]
        public class Result_SetTrackerState : ICapnpSerializable
        {
            public const UInt64 typeId = 0xa6e2e803d7dd6d34UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xae646cc2ccf7f5b6UL)]
        public class Params_UpdateTracker : ICapnpSerializable
        {
            public const UInt64 typeId = 0xae646cc2ccf7f5b6UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                Tracker = CapnpSerializable.Create<Amethyst.Driver.TrackerBase>(reader.Tracker);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                Tracker?.serialize(writer.Tracker);
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public Amethyst.Driver.TrackerBase Tracker
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public Amethyst.Driver.TrackerBase.READER Tracker => ctx.ReadStruct(0, Amethyst.Driver.TrackerBase.READER.create);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 1);
                }

                public Amethyst.Driver.TrackerBase.WRITER Tracker
                {
                    get => BuildPointer<Amethyst.Driver.TrackerBase.WRITER>(0);
                    set => Link(0, value);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xf0dfd5af091a2dd0UL)]
        public class Result_UpdateTracker : ICapnpSerializable
        {
            public const UInt64 typeId = 0xf0dfd5af091a2dd0UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xbb78c819728c6dd5UL)]
        public class Params_RequestVrRestart : ICapnpSerializable
        {
            public const UInt64 typeId = 0xbb78c819728c6dd5UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                Message = reader.Message;
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                writer.Message = Message;
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public string Message
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public string Message => ctx.ReadText(0, null);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 1);
                }

                public string Message
                {
                    get => this.ReadText(0, null);
                    set => this.WriteText(0, value, null);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xa1fc251c6fdac3e3UL)]
        public class Result_RequestVrRestart : ICapnpSerializable
        {
            public const UInt64 typeId = 0xa1fc251c6fdac3e3UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xe3dcab28cf264bfdUL)]
        public class Params_PingDriverService : ICapnpSerializable
        {
            public const UInt64 typeId = 0xe3dcab28cf264bfdUL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xed54258524948644UL)]
        public class Result_PingDriverService : ICapnpSerializable
        {
            public const UInt64 typeId = 0xed54258524948644UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                Ms = reader.Ms;
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                writer.Ms = Ms;
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public long Ms
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public long Ms => ctx.ReadDataLong(0UL, 0L);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(1, 0);
                }

                public long Ms
                {
                    get => this.ReadDataLong(0UL, 0L);
                    set => this.WriteData(0UL, value, 0L);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xc085324cbbcf18ebUL)]
        public class Params_SetDriverPose : ICapnpSerializable
        {
            public const UInt64 typeId = 0xc085324cbbcf18ebUL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                Id = reader.Id;
                Pose = CapnpSerializable.Create<Amethyst.Driver.DriverPose>(reader.Pose);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                writer.Id = Id;
                Pose?.serialize(writer.Pose);
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public uint Id
            {
                get;
                set;
            }

            public Amethyst.Driver.DriverPose Pose
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public uint Id => ctx.ReadDataUInt(0UL, 0U);
                public Amethyst.Driver.DriverPose.READER Pose => ctx.ReadStruct(0, Amethyst.Driver.DriverPose.READER.create);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(1, 1);
                }

                public uint Id
                {
                    get => this.ReadDataUInt(0UL, 0U);
                    set => this.WriteData(0UL, value, 0U);
                }

                public Amethyst.Driver.DriverPose.WRITER Pose
                {
                    get => BuildPointer<Amethyst.Driver.DriverPose.WRITER>(0);
                    set => Link(0, value);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xc95098ffec3d7a6dUL)]
        public class Result_SetDriverPose : ICapnpSerializable
        {
            public const UInt64 typeId = 0xc95098ffec3d7a6dUL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xf0cd61a90d4b9762UL)]
        public class Params_EnableOverride : ICapnpSerializable
        {
            public const UInt64 typeId = 0xf0cd61a90d4b9762UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                Id = reader.Id;
                IsEnabled = reader.IsEnabled;
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                writer.Id = Id;
                writer.IsEnabled = IsEnabled;
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public uint Id
            {
                get;
                set;
            }

            public bool IsEnabled
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public uint Id => ctx.ReadDataUInt(0UL, 0U);
                public bool IsEnabled => ctx.ReadDataBool(32UL, false);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(1, 0);
                }

                public uint Id
                {
                    get => this.ReadDataUInt(0UL, 0U);
                    set => this.WriteData(0UL, value, 0U);
                }

                public bool IsEnabled
                {
                    get => this.ReadDataBool(32UL, false);
                    set => this.WriteData(32UL, value, false);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xfec36c97ea73b10cUL)]
        public class Result_EnableOverride : ICapnpSerializable
        {
            public const UInt64 typeId = 0xfec36c97ea73b10cUL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xbd71fb1318b85b52UL)]
        public class Params_UpdateInputBoolean : ICapnpSerializable
        {
            public const UInt64 typeId = 0xbd71fb1318b85b52UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                Tracker = reader.Tracker;
                Path = reader.Path;
                Value = reader.Value;
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                writer.Tracker = Tracker;
                writer.Path = Path;
                writer.Value = Value;
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public Amethyst.Driver.TrackerType Tracker
            {
                get;
                set;
            }

            public string Path
            {
                get;
                set;
            }

            public bool Value
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public Amethyst.Driver.TrackerType Tracker => (Amethyst.Driver.TrackerType)ctx.ReadDataUShort(0UL, (ushort)0);
                public string Path => ctx.ReadText(0, null);
                public bool Value => ctx.ReadDataBool(16UL, false);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(1, 1);
                }

                public Amethyst.Driver.TrackerType Tracker
                {
                    get => (Amethyst.Driver.TrackerType)this.ReadDataUShort(0UL, (ushort)0);
                    set => this.WriteData(0UL, (ushort)value, (ushort)0);
                }

                public string Path
                {
                    get => this.ReadText(0, null);
                    set => this.WriteText(0, value, null);
                }

                public bool Value
                {
                    get => this.ReadDataBool(16UL, false);
                    set => this.WriteData(16UL, value, false);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xddd400b7f256dddfUL)]
        public class Result_UpdateInputBoolean : ICapnpSerializable
        {
            public const UInt64 typeId = 0xddd400b7f256dddfUL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xd0fcd06aca8cb8beUL)]
        public class Params_UpdateInputScalar : ICapnpSerializable
        {
            public const UInt64 typeId = 0xd0fcd06aca8cb8beUL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                Tracker = reader.Tracker;
                Path = reader.Path;
                Value = reader.Value;
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
                writer.Tracker = Tracker;
                writer.Path = Path;
                writer.Value = Value;
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public Amethyst.Driver.TrackerType Tracker
            {
                get;
                set;
            }

            public string Path
            {
                get;
                set;
            }

            public float Value
            {
                get;
                set;
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
                public Amethyst.Driver.TrackerType Tracker => (Amethyst.Driver.TrackerType)ctx.ReadDataUShort(0UL, (ushort)0);
                public string Path => ctx.ReadText(0, null);
                public float Value => ctx.ReadDataFloat(32UL, 0F);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(1, 1);
                }

                public Amethyst.Driver.TrackerType Tracker
                {
                    get => (Amethyst.Driver.TrackerType)this.ReadDataUShort(0UL, (ushort)0);
                    set => this.WriteData(0UL, (ushort)value, (ushort)0);
                }

                public string Path
                {
                    get => this.ReadText(0, null);
                    set => this.WriteText(0, value, null);
                }

                public float Value
                {
                    get => this.ReadDataFloat(32UL, 0F);
                    set => this.WriteData(32UL, value, 0F);
                }
            }
        }

        [System.CodeDom.Compiler.GeneratedCode("capnpc-csharp", "1.3.0.0"), TypeId(0xdcfd5aab885656a6UL)]
        public class Result_UpdateInputScalar : ICapnpSerializable
        {
            public const UInt64 typeId = 0xdcfd5aab885656a6UL;
            void ICapnpSerializable.Deserialize(DeserializerState arg_)
            {
                var reader = READER.create(arg_);
                applyDefaults();
            }

            public void serialize(WRITER writer)
            {
            }

            void ICapnpSerializable.Serialize(SerializerState arg_)
            {
                serialize(arg_.Rewrap<WRITER>());
            }

            public void applyDefaults()
            {
            }

            public struct READER
            {
                readonly DeserializerState ctx;
                public READER(DeserializerState ctx)
                {
                    this.ctx = ctx;
                }

                public static READER create(DeserializerState ctx) => new READER(ctx);
                public static implicit operator DeserializerState(READER reader) => reader.ctx;
                public static implicit operator READER(DeserializerState ctx) => new READER(ctx);
            }

            public class WRITER : SerializerState
            {
                public WRITER()
                {
                    this.SetStruct(0, 0);
                }
            }
        }
    }
}