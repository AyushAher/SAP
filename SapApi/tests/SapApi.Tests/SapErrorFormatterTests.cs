using System.Net;
using FluentAssertions;
using SapApi.Shared;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests;

[TestFixture]
public class SapErrorFormatterTests
{
    [Test]
    public void Format_UsesSapMessageValue()
    {
        var sap = new SapBaseResponse
        {
            Error = new SapError
            {
                Code = -5002,
                Message = new SapMessage { Value = "Item code is missing" },
            },
        };

        SapErrorFormatter.Format(sap, "{}", HttpStatusCode.BadRequest)
            .Should().Be("Item code is missing");
    }

    [Test]
    public void Format_FallsBackToRawJsonMessage()
    {
        var json = """{"error":{"code":-10,"message":{"lang":"en-us","value":"Invalid BP"}}}""";

        SapErrorFormatter.Format(null, json, HttpStatusCode.BadRequest)
            .Should().Be("Invalid BP");
    }

    [Test]
    public void Format_UsesStatusWhenNoMessage()
    {
        SapErrorFormatter.Format(null, null, HttpStatusCode.BadRequest)
            .Should().Be("SAP Service Layer request failed (400).");
    }
}
