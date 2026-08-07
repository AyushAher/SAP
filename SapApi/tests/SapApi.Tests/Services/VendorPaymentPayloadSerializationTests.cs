using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using SapApi.Shared;
using SapApi.Shared.Requests;

namespace SapApi.Tests.Services;

[TestFixture]
public class VendorPaymentPayloadSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Test]
    public void CreateVendorPayments_Payload_Omits_U_BSC_3()
    {
        var request = new SapVendorPaymentRequests
        {
            CardCode = "V001",
            TransferSum = "21694.00",
            TransferReference = "M883629",
            CounterReference = "M883629",
            TransferAccount = "_SYS00000000980",
            TransferDate = DateTime.UtcNow,
            BPLId = 1,
        };

        var json = JsonSerializer.Serialize(request, Options);

        json.Should().NotContain("U_BSC_3");
        json.Should().NotContain("PaymentRequestId");
        json.Should().Contain("CardCode");
        json.Should().Contain("TransferSum");
    }

    [Test]
    public void CreateVendorPayments_Payload_Includes_Remarks_And_JournalRemarks()
    {
        var remarks = Constants.PaymentRemarks.Build("Release after inspection", 1, "1234");
        var request = new SapVendorPaymentRequests
        {
            CardCode = "V001",
            TransferSum = "100.00",
            TransferReference = "R1",
            CounterReference = "R1",
            TransferAccount = "_SYS00000000980",
            TransferDate = DateTime.UtcNow,
            Remarks = remarks,
            JournalRemarks = remarks,
            BPLId = 1,
        };

        var json = JsonSerializer.Serialize(request, Options);

        json.Should().Contain("Remarks");
        json.Should().Contain("JournalRemarks");
        json.Should().Contain("Release after inspection");
        json.Should().NotContain("U_BSC_3");
    }
}
