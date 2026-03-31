namespace Origo.Core.DataSource.Converters;

internal sealed class StringDataSourceConverter : DataSourceConverter<string>
{
    public override string Read(DataSourceNode node) => node.AsString();
    public override DataSourceNode Write(string value) => DataSourceNode.CreateString(value);
}

internal sealed class Int32DataSourceConverter : DataSourceConverter<int>
{
    public override int Read(DataSourceNode node) => node.AsInt();
    public override DataSourceNode Write(int value) => DataSourceNode.CreateNumber(value);
}

internal sealed class Int64DataSourceConverter : DataSourceConverter<long>
{
    public override long Read(DataSourceNode node) => node.AsLong();
    public override DataSourceNode Write(long value) => DataSourceNode.CreateNumber(value);
}

internal sealed class SingleDataSourceConverter : DataSourceConverter<float>
{
    public override float Read(DataSourceNode node) => node.AsFloat();
    public override DataSourceNode Write(float value) => DataSourceNode.CreateNumber(value);
}

internal sealed class DoubleDataSourceConverter : DataSourceConverter<double>
{
    public override double Read(DataSourceNode node) => node.AsDouble();
    public override DataSourceNode Write(double value) => DataSourceNode.CreateNumber(value);
}

internal sealed class BooleanDataSourceConverter : DataSourceConverter<bool>
{
    public override bool Read(DataSourceNode node) => node.AsBool();
    public override DataSourceNode Write(bool value) => DataSourceNode.CreateBoolean(value);
}
