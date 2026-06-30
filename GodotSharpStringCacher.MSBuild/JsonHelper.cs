
using System.IO;
using System.Runtime.Serialization.Json;

/// <summary>
/// Helper for JSON serializing using <see cref="System.Runtime.Serialization.DataContractAttribute"/>s.
/// Decrepit technology, but has the advantage of being embedded into .NETStandard.
/// </summary>
public static class JsonHelper
{
    public static void Serialize<T>(T obj, Stream stream)
    {
		var serializer = new DataContractJsonSerializer(typeof(T));
		serializer.WriteObject(stream, obj);
    }

    public static T Deserialize<T>(Stream stream)
    {
		var serializer = new DataContractJsonSerializer(typeof(T));
		return (T)serializer.ReadObject(stream);
    }
}
