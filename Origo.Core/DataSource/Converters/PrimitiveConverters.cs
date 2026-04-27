using System.Globalization;

namespace Origo.Core.DataSource.Converters;

internal sealed class StringDataSourceConverter : DataSourceConverter<string>
{
    public override string Read(DataSourceNode node)
    {
        return node.AsString();
    }

    public override DataSourceNode Write(string value)
    {
        return DataSourceNode.CreateString(value);
    }
}

internal sealed class ByteDataSourceConverter : DataSourceConverter<byte>
{
    public override byte Read(DataSourceNode node)
    {
        return node.AsByte();
    }

    public override DataSourceNode Write(byte value)
    {
        return DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class SByteDataSourceConverter : DataSourceConverter<sbyte>
{
    public override sbyte Read(DataSourceNode node)
    {
        return node.AsSByte();
    }

    public override DataSourceNode Write(sbyte value)
    {
        return DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class Int16DataSourceConverter : DataSourceConverter<short>
{
    public override short Read(DataSourceNode node)
    {
        return node.AsShort();
    }

    public override DataSourceNode Write(short value)
    {
        return DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class UInt16DataSourceConverter : DataSourceConverter<ushort>
{
    public override ushort Read(DataSourceNode node)
    {
        return node.AsUShort();
    }

    public override DataSourceNode Write(ushort value)
    {
        return DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class Int32DataSourceConverter : DataSourceConverter<int>
{
    public override int Read(DataSourceNode node)
    {
        return node.AsInt();
    }

    public override DataSourceNode Write(int value)
    {
        return DataSourceNode.CreateNumber(value);
    }
}

internal sealed class UInt32DataSourceConverter : DataSourceConverter<uint>
{
    public override uint Read(DataSourceNode node)
    {
        return node.AsUInt();
    }

    public override DataSourceNode Write(uint value)
    {
        return DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class Int64DataSourceConverter : DataSourceConverter<long>
{
    public override long Read(DataSourceNode node)
    {
        return node.AsLong();
    }

    public override DataSourceNode Write(long value)
    {
        return DataSourceNode.CreateNumber(value);
    }
}

internal sealed class UInt64DataSourceConverter : DataSourceConverter<ulong>
{
    public override ulong Read(DataSourceNode node)
    {
        return node.AsULong();
    }

    public override DataSourceNode Write(ulong value)
    {
        return DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class SingleDataSourceConverter : DataSourceConverter<float>
{
    public override float Read(DataSourceNode node)
    {
        return node.AsFloat();
    }

    public override DataSourceNode Write(float value)
    {
        return DataSourceNode.CreateNumber(value);
    }
}

internal sealed class DoubleDataSourceConverter : DataSourceConverter<double>
{
    public override double Read(DataSourceNode node)
    {
        return node.AsDouble();
    }

    public override DataSourceNode Write(double value)
    {
        return DataSourceNode.CreateNumber(value);
    }
}

internal sealed class DecimalDataSourceConverter : DataSourceConverter<decimal>
{
    public override decimal Read(DataSourceNode node)
    {
        return node.AsDecimal();
    }

    public override DataSourceNode Write(decimal value)
    {
        return DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class CharDataSourceConverter : DataSourceConverter<char>
{
    public override char Read(DataSourceNode node)
    {
        return node.AsChar();
    }

    public override DataSourceNode Write(char value)
    {
        return DataSourceNode.CreateString(value.ToString());
    }
}

internal sealed class BooleanDataSourceConverter : DataSourceConverter<bool>
{
    public override bool Read(DataSourceNode node)
    {
        return node.AsBool();
    }

    public override DataSourceNode Write(bool value)
    {
        return DataSourceNode.CreateBoolean(value);
    }
}