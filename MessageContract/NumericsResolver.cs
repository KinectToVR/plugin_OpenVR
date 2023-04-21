using System.Numerics;
using MessagePack;
using MessagePack.Formatters;

namespace MessageContract;

public class NumericsResolver : IFormatterResolver
{
    public static readonly NumericsResolver Instance = new();

    private NumericsResolver()
    {
    }

    public IMessagePackFormatter<T>? GetFormatter<T>()
    {
        return FormatterCache<T>.Formatter;
    }

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter;

        static FormatterCache()
        {
            Formatter = (IMessagePackFormatter<T>?)NumericsResolveryResolverGetFormatterHelper.GetFormatter(typeof(T));
        }
    }
}

internal static class NumericsResolveryResolverGetFormatterHelper
{
    private static readonly Dictionary<Type, object> FormatterMap = new()
    {
        // standard
        { typeof(Vector2), new Vector2Formatter() },
        { typeof(Vector3), new Vector3Formatter() },
        { typeof(Vector4), new Vector4Formatter() },
        { typeof(Quaternion), new QuaternionFormatter() },
        { typeof(Vector2?), new StaticNullableFormatter<Vector2>(new Vector2Formatter()) },
        { typeof(Vector3?), new StaticNullableFormatter<Vector3>(new Vector3Formatter()) },
        { typeof(Vector4?), new StaticNullableFormatter<Vector4>(new Vector4Formatter()) },
        { typeof(Quaternion?), new StaticNullableFormatter<Quaternion>(new QuaternionFormatter()) },

        // standard + array
        { typeof(Vector2[]), new ArrayFormatter<Vector2>() },
        { typeof(Vector3[]), new ArrayFormatter<Vector3>() },
        { typeof(Vector4[]), new ArrayFormatter<Vector4>() },
        { typeof(Quaternion[]), new ArrayFormatter<Quaternion>() },
        { typeof(Vector2?[]), new ArrayFormatter<Vector2?>() },
        { typeof(Vector3?[]), new ArrayFormatter<Vector3?>() },
        { typeof(Vector4?[]), new ArrayFormatter<Vector4?>() },
        { typeof(Quaternion?[]), new ArrayFormatter<Quaternion?>() },

        // standard + list
        { typeof(List<Vector2>), new ListFormatter<Vector2>() },
        { typeof(List<Vector3>), new ListFormatter<Vector3>() },
        { typeof(List<Vector4>), new ListFormatter<Vector4>() },
        { typeof(List<Quaternion>), new ListFormatter<Quaternion>() },
        { typeof(List<Vector2?>), new ListFormatter<Vector2?>() },
        { typeof(List<Vector3?>), new ListFormatter<Vector3?>() },
        { typeof(List<Vector4?>), new ListFormatter<Vector4?>() },
        { typeof(List<Quaternion?>), new ListFormatter<Quaternion?>() },

        // new
        { typeof(Matrix4x4), new Matrix4x4Formatter() },
        { typeof(Matrix4x4?), new StaticNullableFormatter<Matrix4x4>(new Matrix4x4Formatter()) },

        // new + array
        { typeof(Matrix4x4[]), new ArrayFormatter<Matrix4x4>() },
        { typeof(Matrix4x4?[]), new ArrayFormatter<Matrix4x4?>() },

        // new + list
        { typeof(List<Matrix4x4>), new ListFormatter<Matrix4x4>() },
        { typeof(List<Matrix4x4?>), new ListFormatter<Matrix4x4?>() }
    };

    internal static object? GetFormatter(Type t)
    {
        object formatter;
        if (FormatterMap.TryGetValue(t, out formatter)) return formatter;

        return null;
    }
}

public sealed class Vector2Formatter : IMessagePackFormatter<Vector2>
{
    public void Serialize(ref MessagePackWriter writer, Vector2 value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    public Vector2 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

        var length = reader.ReadArrayHeader();
        var x = default(float);
        var y = default(float);
        for (var i = 0; i < length; i++)
        {
            var key = i;
            switch (key)
            {
                case 0:
                    x = reader.ReadSingle();
                    break;
                case 1:
                    y = reader.ReadSingle();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        var result = new Vector2(x, y);
        return result;
    }
}

public sealed class Vector3Formatter : IMessagePackFormatter<Vector3>
{
    public void Serialize(ref MessagePackWriter writer, Vector3 value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(3);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    public Vector3 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

        var length = reader.ReadArrayHeader();
        var x = default(float);
        var y = default(float);
        var z = default(float);
        for (var i = 0; i < length; i++)
        {
            var key = i;
            switch (key)
            {
                case 0:
                    x = reader.ReadSingle();
                    break;
                case 1:
                    y = reader.ReadSingle();
                    break;
                case 2:
                    z = reader.ReadSingle();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        var result = new Vector3(x, y, z);
        return result;
    }
}

public sealed class Vector4Formatter : IMessagePackFormatter<Vector4>
{
    public void Serialize(ref MessagePackWriter writer, Vector4 value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(4);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    public Vector4 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

        var length = reader.ReadArrayHeader();
        var x = default(float);
        var y = default(float);
        var z = default(float);
        var w = default(float);
        for (var i = 0; i < length; i++)
        {
            var key = i;
            switch (key)
            {
                case 0:
                    x = reader.ReadSingle();
                    break;
                case 1:
                    y = reader.ReadSingle();
                    break;
                case 2:
                    z = reader.ReadSingle();
                    break;
                case 3:
                    w = reader.ReadSingle();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        var result = new Vector4(x, y, z, w);
        return result;
    }
}

public sealed class QuaternionFormatter : IMessagePackFormatter<Quaternion>
{
    public void Serialize(ref MessagePackWriter writer, Quaternion value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(4);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    public Quaternion Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

        var length = reader.ReadArrayHeader();
        var x = default(float);
        var y = default(float);
        var z = default(float);
        var w = default(float);
        for (var i = 0; i < length; i++)
        {
            var key = i;
            switch (key)
            {
                case 0:
                    x = reader.ReadSingle();
                    break;
                case 1:
                    y = reader.ReadSingle();
                    break;
                case 2:
                    z = reader.ReadSingle();
                    break;
                case 3:
                    w = reader.ReadSingle();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        var result = new Quaternion(x, y, z, w);
        return result;
    }
}

public sealed class Matrix4x4Formatter : IMessagePackFormatter<Matrix4x4>
{
    public void Serialize(ref MessagePackWriter writer, Matrix4x4 value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(16);
        writer.Write(value.M11);
        writer.Write(value.M21);
        writer.Write(value.M31);
        writer.Write(value.M41);
        writer.Write(value.M12);
        writer.Write(value.M22);
        writer.Write(value.M32);
        writer.Write(value.M42);
        writer.Write(value.M13);
        writer.Write(value.M23);
        writer.Write(value.M33);
        writer.Write(value.M43);
        writer.Write(value.M14);
        writer.Write(value.M24);
        writer.Write(value.M34);
        writer.Write(value.M44);
    }

    public Matrix4x4 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

        var length = reader.ReadArrayHeader();
        var __m00__ = default(float);
        var __m10__ = default(float);
        var __m20__ = default(float);
        var __m30__ = default(float);
        var __m01__ = default(float);
        var __m11__ = default(float);
        var __m21__ = default(float);
        var __m31__ = default(float);
        var __m02__ = default(float);
        var __m12__ = default(float);
        var __m22__ = default(float);
        var __m32__ = default(float);
        var __m03__ = default(float);
        var __m13__ = default(float);
        var __m23__ = default(float);
        var __m33__ = default(float);
        for (var i = 0; i < length; i++)
        {
            var key = i;
            switch (key)
            {
                case 0:
                    __m00__ = reader.ReadSingle();
                    break;
                case 1:
                    __m10__ = reader.ReadSingle();
                    break;
                case 2:
                    __m20__ = reader.ReadSingle();
                    break;
                case 3:
                    __m30__ = reader.ReadSingle();
                    break;
                case 4:
                    __m01__ = reader.ReadSingle();
                    break;
                case 5:
                    __m11__ = reader.ReadSingle();
                    break;
                case 6:
                    __m21__ = reader.ReadSingle();
                    break;
                case 7:
                    __m31__ = reader.ReadSingle();
                    break;
                case 8:
                    __m02__ = reader.ReadSingle();
                    break;
                case 9:
                    __m12__ = reader.ReadSingle();
                    break;
                case 10:
                    __m22__ = reader.ReadSingle();
                    break;
                case 11:
                    __m32__ = reader.ReadSingle();
                    break;
                case 12:
                    __m03__ = reader.ReadSingle();
                    break;
                case 13:
                    __m13__ = reader.ReadSingle();
                    break;
                case 14:
                    __m23__ = reader.ReadSingle();
                    break;
                case 15:
                    __m33__ = reader.ReadSingle();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        var ____result = default(Matrix4x4);
        ____result.M11 = __m00__;
        ____result.M21 = __m10__;
        ____result.M31 = __m20__;
        ____result.M41 = __m30__;
        ____result.M12 = __m01__;
        ____result.M22 = __m11__;
        ____result.M32 = __m21__;
        ____result.M42 = __m31__;
        ____result.M13 = __m02__;
        ____result.M23 = __m12__;
        ____result.M33 = __m22__;
        ____result.M43 = __m32__;
        ____result.M14 = __m03__;
        ____result.M24 = __m13__;
        ____result.M34 = __m23__;
        ____result.M44 = __m33__;
        return ____result;
    }
}