using System.Text.Json;
using FluentAssertions;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Serialization;

namespace SapApi.Tests.Serialization;

[TestFixture]
public class FlexibleStringJsonConverterTests
{
    [Test]
    public void Reads_Number_As_String()
    {
        var item = JsonSerializer.Deserialize<ItemsResponse>("""{"ItemCode":"I1","ChapterID":24}""");
        item!.ChapterID.Should().Be("24");
    }

    [Test]
    public void Reads_String_As_String()
    {
        var item = JsonSerializer.Deserialize<ItemsResponse>("""{"ItemCode":"I1","ChapterID":"73.07.99"}""");
        item!.ChapterID.Should().Be("73.07.99");
    }

    [Test]
    public void Converter_RoundTrips_Null()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new FlexibleStringJsonConverter());
        JsonSerializer.Deserialize<string?>("null", options).Should().BeNull();
    }
}
