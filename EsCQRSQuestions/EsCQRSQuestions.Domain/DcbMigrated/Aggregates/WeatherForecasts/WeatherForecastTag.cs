using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.DcbTags;

public record WeatherForecastTag(Guid WeatherForecastId) : IGuidTagGroup<WeatherForecastTag>
{
    public bool IsConsistencyTag() => true;
    public static string TagGroupName => "WeatherForecast";
    public static WeatherForecastTag FromContent(string content) => new(Guid.Parse(content));
    public Guid GetId() => WeatherForecastId;
}
