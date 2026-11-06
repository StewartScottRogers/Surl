using System.Text;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// One value in a field table or field array: its type octet and the value, boxed as the
/// factory for its type makes it (<see cref="bool"/>, <see cref="sbyte"/>, <see cref="byte"/>,
/// <see cref="short"/>, <see cref="ushort"/>, <see cref="int"/>, <see cref="uint"/>,
/// <see cref="long"/>, <see cref="float"/>, <see cref="double"/>, <see cref="Amqp0Decimal"/>,
/// a <see cref="byte"/> array for long strings and byte arrays, an
/// <see cref="IReadOnlyList{T}"/> of values for arrays, <see cref="ulong"/> for timestamps, an
/// <see cref="Amqp0FieldTable"/> for tables, and <see langword="null"/> for void).
/// </summary>
public sealed class Amqp0FieldValue
{
    private Amqp0FieldValue(Amqp0FieldValueType type, object? value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>The value's type octet.</summary>
    public Amqp0FieldValueType Type { get; }

    /// <summary>The value, boxed as its type says.</summary>
    public object? Value { get; }

    /// <summary>The void value (<c>V</c>).</summary>
    public static Amqp0FieldValue Void { get; } = new(Amqp0FieldValueType.Void, null);

    /// <summary>A boolean (<c>t</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Boolean(bool value) => new(Amqp0FieldValueType.Boolean, value);

    /// <summary>A signed 8-bit integer (<c>b</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Int8(sbyte value) => new(Amqp0FieldValueType.Int8, value);

    /// <summary>An unsigned 8-bit integer (<c>B</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue UInt8(byte value) => new(Amqp0FieldValueType.UInt8, value);

    /// <summary>A signed 16-bit integer (<c>s</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Int16(short value) => new(Amqp0FieldValueType.Int16, value);

    /// <summary>An unsigned 16-bit integer (<c>u</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue UInt16(ushort value) => new(Amqp0FieldValueType.UInt16, value);

    /// <summary>A signed 32-bit integer (<c>I</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Int32(int value) => new(Amqp0FieldValueType.Int32, value);

    /// <summary>An unsigned 32-bit integer (<c>i</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue UInt32(uint value) => new(Amqp0FieldValueType.UInt32, value);

    /// <summary>A signed 64-bit integer under RabbitMQ's octet <c>l</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Int64(long value) => new(Amqp0FieldValueType.Int64, value);

    /// <summary>A signed 64-bit integer under the PDF's octet <c>L</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Int64SpecificationOctet(long value) => new(Amqp0FieldValueType.Int64SpecificationOctet, value);

    /// <summary>A single-precision float (<c>f</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Float32(float value) => new(Amqp0FieldValueType.Float32, value);

    /// <summary>A double-precision float (<c>d</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Float64(double value) => new(Amqp0FieldValueType.Float64, value);

    /// <summary>A decimal (<c>D</c>).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Decimal(Amqp0Decimal value) => new(Amqp0FieldValueType.Decimal, value);

    /// <summary>A long string (<c>S</c>) of raw octets.</summary>
    /// <param name="value">The octets; copied.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue LongString(ReadOnlySpan<byte> value) => new(Amqp0FieldValueType.LongString, value.ToArray());

    /// <summary>A long string (<c>S</c>) holding <paramref name="text"/> in UTF-8.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue LongString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(Amqp0FieldValueType.LongString, Encoding.UTF8.GetBytes(text));
    }

    /// <summary>A byte array (<c>x</c>).</summary>
    /// <param name="value">The octets; copied.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue ByteArray(ReadOnlySpan<byte> value) => new(Amqp0FieldValueType.ByteArray, value.ToArray());

    /// <summary>A field array (<c>A</c>).</summary>
    /// <param name="values">The values, in order.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Array(IReadOnlyList<Amqp0FieldValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(Amqp0FieldValueType.Array, values);
    }

    /// <summary>A timestamp (<c>T</c>), seconds since the POSIX epoch.</summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Timestamp(ulong seconds) => new(Amqp0FieldValueType.Timestamp, seconds);

    /// <summary>A nested field table (<c>F</c>).</summary>
    /// <param name="table">The table.</param>
    /// <returns>The field value.</returns>
    public static Amqp0FieldValue Table(Amqp0FieldTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return new(Amqp0FieldValueType.Table, table);
    }
}
