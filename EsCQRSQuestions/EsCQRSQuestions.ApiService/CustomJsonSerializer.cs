using System.Text.Json;
using Newtonsoft.Json;
using Orleans.Storage;
using JsonSerializer = System.Text.Json.JsonSerializer;

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
        var result = JsonSerializer.Deserialize<T>(input.ToStream(), _options);
        return result is null
            ? throw new System.Text.Json.JsonException($"Failed to deserialize {typeof(T).FullName} from grain storage payload.")
            : result;
    }
}
public class NewtonsoftJsonSerializer : IGrainStorageSerializer
{
    private readonly JsonSerializerSettings _settings;

    public NewtonsoftJsonSerializer()
    {
        _settings = new JsonSerializerSettings
        {
            // Similar to IncludeFields = true in System.Text.Json
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {

            }
        };
    }

    public BinaryData Serialize<T>(T input)
    {
        string json = JsonConvert.SerializeObject(input, _settings);
        return BinaryData.FromString(json);
    }

    public T Deserialize<T>(BinaryData input)
    {
        string json = input.ToString();
        var result = JsonConvert.DeserializeObject<T>(json, _settings);
        return result is null
            ? throw new JsonSerializationException($"Failed to deserialize {typeof(T).FullName} from grain storage payload.")
            : result;
    }
}
