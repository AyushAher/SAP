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
    public void Format_ClarifiesOdbc2028()
    {
        var sap = new SapBaseResponse
        {
            Error = new SapError
            {
                Code = -2028,
                Message = new SapMessage { Value = "No matching records found (ODBC -2028)" },
            },
        };

        var message = SapErrorFormatter.Format(sap, "{}", HttpStatusCode.BadRequest);
        message.Should().StartWith("No matching records found (ODBC -2028)");
        message.Should().Contain("numbering series");
    }

    [Test]
    public void Format_ClarifiesMissingUserDefaultNumberingSeries()
    {
        var sap = new SapBaseResponse
        {
            Error = new SapError
            {
                Code = 131,
                Message = new SapMessage
                {
                    Value = "To generate this document, first define the numbering series in the Administration module",
                },
            },
        };

        var message = SapErrorFormatter.Format(sap, "{}", HttpStatusCode.BadRequest);
        message.Should().Contain("define the numbering series");
        message.Should().Contain("default numbering series");
        message.Should().Contain("Document Numbering");
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
