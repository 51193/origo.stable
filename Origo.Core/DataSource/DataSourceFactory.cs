using Origo.Core.DataSource.Codec;
using Origo.Core.DataSource.Converters;
using Origo.Core.Serialization;

namespace Origo.Core.DataSource;

/// <summary>
///     创建预配置的 <see cref="DataSourceConverterRegistry" /> 与编解码器实例的工厂。
/// </summary>
public static class DataSourceFactory
{
    public static DataSourceConverterRegistry CreateDefaultRegistry(TypeStringMapping typeMapping)
    {
        var registry = new DataSourceConverterRegistry();

        // Primitives
        registry.Register(new StringDataSourceConverter());
        registry.Register(new Int32DataSourceConverter());
        registry.Register(new Int64DataSourceConverter());
        registry.Register(new SingleDataSourceConverter());
        registry.Register(new DoubleDataSourceConverter());
        registry.Register(new BooleanDataSourceConverter());

        // Domain converters
        var typedDataConverter = new TypedDataConverter(typeMapping, registry);
        registry.Register(typedDataConverter);

        var nodeMetaConverter = new NodeMetaDataConverter();
        var strategyMetaConverter = new StrategyMetaDataConverter();
        var dataMetaConverter = new DataMetaDataConverter(typedDataConverter);
        var sndMetaConverter = new SndMetaDataConverter(
            nodeMetaConverter, strategyMetaConverter, dataMetaConverter);

        registry.Register(nodeMetaConverter);
        registry.Register(strategyMetaConverter);
        registry.Register(dataMetaConverter);
        registry.Register(sndMetaConverter);

        registry.Register(new SndMetaDataListConverter(sndMetaConverter));
        registry.Register(new BlackboardDataConverter(typedDataConverter));
        registry.Register(new StringDictionaryConverter());
        registry.Register(new StateMachineContainerPayloadConverter());

        return registry;
    }

    public static IDataSourceCodec CreateJsonCodec(bool writeIndented = true) => new JsonDataSourceCodec(writeIndented);

    public static IDataSourceCodec CreateMapCodec() => new MapDataSourceCodec();
}
