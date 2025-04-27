using System.Text.Json;
using Orleans.Storage;

public class CustomJsonSerializer : IGrainStorageSerializer
{
    private readonly JsonSerializerOptions? _options;

    public CustomJsonSerializer()
    {
        _options = new JsonSerializerOptions
        {
            IncludeFields = true,
        };
    }

    public BinaryData Serialize<T>(T input)
    {
        return new BinaryData(JsonSerializer.SerializeToUtf8Bytes(input, _options));
    }

    public T Deserialize<T>(BinaryData input)
    {
        return JsonSerializer.Deserialize<T>(input.ToStream(), _options);
    }
}