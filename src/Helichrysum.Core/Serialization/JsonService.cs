namespace Helichrysum.Core.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

/// <summary>
/// Central JSON serialization facade. All JSON operations in the codebase must
/// go through this service — never reference a JSON library directly in feature code.
/// This isolates the underlying JSON dependency so it can be swapped or
/// conditionally compiled (e.g. for AOT) without touching feature code.
/// </summary>
public static class JsonService
{
    private static readonly JsonSerializerSettings DefaultSettings = CreateSettings(indented: false, camelCase: false);

    private static readonly JsonSerializerSettings IndentedSettings = CreateSettings(indented: true, camelCase: false);

    private static readonly JsonSerializerSettings CamelCaseSettings = CreateSettings(indented: true, camelCase: true);

    private static JsonSerializerSettings CreateSettings(bool indented, bool camelCase)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = indented ? Formatting.Indented : Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
        };

        if (camelCase)
        {
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        }

        return settings;
    }

    /// <summary>
    /// Serializes an object to a compact JSON string.
    /// </summary>
    public static string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, DefaultSettings);
    }

    /// <summary>
    /// Serializes an object to an indented (human-readable) JSON string.
    /// </summary>
    public static string SerializeIndented(object value)
    {
        return JsonConvert.SerializeObject(value, IndentedSettings);
    }

    /// <summary>
    /// Serializes to indented JSON with camelCase property names.
    /// </summary>
    public static string SerializeCamelCase(object value)
    {
        return JsonConvert.SerializeObject(value, CamelCaseSettings);
    }

    /// <summary>
    /// Deserializes a JSON string to an object of type <typeparamref name="T"/>.
    /// </summary>
    public static T? Deserialize<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, DefaultSettings);
    }
}