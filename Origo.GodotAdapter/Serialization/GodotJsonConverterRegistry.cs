using System.Text.Json;
using Godot;
using Origo.Core.Serialization;

namespace Origo.GodotAdapter.Serialization;

/// <summary>
///     一站式注册所有 Godot 内置类型的 <see cref="TypeStringMapping" /> 映射与 JsonConverter。
/// </summary>
public static class GodotJsonConverterRegistry
{
    public static void RegisterTypeMappings(TypeStringMapping typeMapping)
    {
        typeMapping.RegisterType<Vector2>(GodotEngineTypeNames.Vector2);
        typeMapping.RegisterType<Vector2I>(GodotEngineTypeNames.Vector2I);
        typeMapping.RegisterType<Vector3>(GodotEngineTypeNames.Vector3);
        typeMapping.RegisterType<Vector3I>(GodotEngineTypeNames.Vector3I);
        typeMapping.RegisterType<Vector4>(GodotEngineTypeNames.Vector4);
        typeMapping.RegisterType<Quaternion>(GodotEngineTypeNames.Quaternion);
        typeMapping.RegisterType<Basis>(GodotEngineTypeNames.Basis);
        typeMapping.RegisterType<Transform2D>(GodotEngineTypeNames.Transform2D);
        typeMapping.RegisterType<Transform3D>(GodotEngineTypeNames.Transform3D);
        typeMapping.RegisterType<Color>(GodotEngineTypeNames.Color);
        typeMapping.RegisterType<Rect2>(GodotEngineTypeNames.Rect2);
        typeMapping.RegisterType<Rect2I>(GodotEngineTypeNames.Rect2I);
        typeMapping.RegisterType<Aabb>(GodotEngineTypeNames.Aabb);
        typeMapping.RegisterType<Plane>(GodotEngineTypeNames.Plane);
    }

    public static void AddConverters(JsonSerializerOptions options)
    {
        options.Converters.Add(new Vector2JsonConverter());
        options.Converters.Add(new Vector2IJsonConverter());
        options.Converters.Add(new Vector3JsonConverter());
        options.Converters.Add(new Vector3IJsonConverter());
        options.Converters.Add(new Vector4JsonConverter());
        options.Converters.Add(new QuaternionJsonConverter());
        options.Converters.Add(new BasisJsonConverter());
        options.Converters.Add(new Transform2DJsonConverter());
        options.Converters.Add(new Transform3DJsonConverter());
        options.Converters.Add(new ColorJsonConverter());
        options.Converters.Add(new Rect2JsonConverter());
        options.Converters.Add(new Rect2IJsonConverter());
        options.Converters.Add(new AabbJsonConverter());
        options.Converters.Add(new PlaneJsonConverter());
    }
}