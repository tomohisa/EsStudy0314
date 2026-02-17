using EsCQRSQuestions.Domain;
using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Commands;
using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;
using EsCQRSQuestions.Domain.DcbTags;
using EsCQRSQuestions.Domain.ValueObjects;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Common;
using Sekiban.Dcb.Events;
using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Unit;

public class WeatherForecastTests
{
    [Fact]
    public async Task InputWeatherForecastCommand_EmitsWeatherForecastInputted()
    {
        var command = new InputWeatherForecastCommand("Tokyo", new DateOnly(2025, 3, 4), new TemperatureCelsius(25), "Sunny");

        var result = await InputWeatherForecastCommand.HandleAsync(command, new NoopCommandContext());

        Assert.True(result.IsSuccess);
        var payload = Assert.IsType<WeatherForecastInputted>(result.GetValue().GetValue().Event);
        Assert.Equal("Tokyo", payload.Location);
    }

    [Fact]
    public void WeatherForecastProjector_ProjectsCreateUpdateDelete()
    {
        var id = Guid.CreateVersion7();
        var created = WeatherForecastProjector.Project(new EmptyTagStatePayload(),
            CreateEvent(new WeatherForecastInputted(id, "Tokyo", new DateOnly(2025, 3, 4), new TemperatureCelsius(20), "Cloudy")));

        var forecast = Assert.IsType<WeatherForecast>(created);
        Assert.Equal("Tokyo", forecast.Location);

        var updated = WeatherForecastProjector.Project(forecast,
            CreateEvent(new WeatherForecastLocationUpdated("Osaka")));
        var updatedForecast = Assert.IsType<WeatherForecast>(updated);
        Assert.Equal("Osaka", updatedForecast.Location);

        var deleted = WeatherForecastProjector.Project(updatedForecast, CreateEvent(new WeatherForecastDeleted()));
        Assert.IsType<DeletedWeatherForecast>(deleted);
    }

    private static Event CreateEvent(IEventPayload payload)
    {
        return new Event(
            payload,
            SortableUniqueId.GenerateNew(),
            payload.GetType().Name,
            Guid.NewGuid(),
            new EventMetadata("test", "test", "test"),
            [new WeatherForecastTag(Guid.CreateVersion7()).ToString()]);
    }

    private class NoopCommandContext : ICommandContext
    {
        public Task<ResultBox<TagStateTyped<TState>>> GetStateAsync<TState, TProjector>(ITag tag)
            where TState : ITagStatePayload where TProjector : ITagProjector<TProjector> =>
            Task.FromResult(ResultBox.FromException<TagStateTyped<TState>>(new NotImplementedException()));

        public Task<ResultBox<TagState>> GetStateAsync<TProjector>(ITag tag) where TProjector : ITagProjector<TProjector> =>
            Task.FromResult(ResultBox.FromException<TagState>(new NotImplementedException()));

        public Task<ResultBox<bool>> TagExistsAsync(ITag tag) => Task.FromResult(ResultBox.FromValue(false));

        public Task<ResultBox<string>> GetTagLatestSortableUniqueIdAsync(ITag tag) =>
            Task.FromResult(ResultBox.FromValue(string.Empty));

        public Task<ResultBox<EventOrNone>> AppendEvent(IEventPayload ev, params ITag[] tags) =>
            Task.FromResult(EventOrNone.EventWithTags(ev, tags));

        public Task<ResultBox<EventOrNone>> AppendEvent(EventPayloadWithTags eventPayloadWithTags) =>
            Task.FromResult(EventOrNone.Event(eventPayloadWithTags));
    }
}
