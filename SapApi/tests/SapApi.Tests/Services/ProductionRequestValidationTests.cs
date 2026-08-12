using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SapApi.Shared.Requests;

namespace SapApi.Tests.Services;

/// <summary>
/// Issue for Production and Receipt from Production post the production order they read from SAP
/// straight back to our API. U_ProdType (ProductionCategory) and U_DwgNo (DrawingNo) are optional
/// SAP user fields that are null on many real orders, so the request model must accept them absent.
/// </summary>
[TestFixture]
public class ProductionRequestValidationTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _services = null!;
    private IObjectModelValidator _validator = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        _services = services.BuildServiceProvider();
        _validator = _services.GetRequiredService<IObjectModelValidator>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _services.Dispose();

    private ModelStateDictionary Validate(SapInventoryGenExitRequestOrderLines model)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        _validator.Validate(actionContext, validationState: null, prefix: string.Empty, model: model);
        return actionContext.ModelState;
    }

    private static string Describe(ModelStateDictionary modelState) =>
        string.Join("; ", modelState.Select(entry =>
            $"{entry.Key}: {string.Join(", ", entry.Value!.Errors.Select(e => e.ErrorMessage))}"));

    /// <summary>Payload shape the browser posts back: SAP field names, optional user fields absent.</summary>
    private const string PayloadWithoutProductionCategory = """
        {
          "ProductionOrder": {
            "AbsoluteEntry": 639,
            "DocumentNumber": 3,
            "ItemNo": "FG-1",
            "ProductionOrderStatus": "boposReleased",
            "PlannedQuantity": 10,
            "ProductionOrderLines": []
          },
          "ProductionOrderLinesEntryNumber": [
            { "LineNumber": 1, "ItemNo": "RM-1", "PlannedQuantity": 5, "IssuedQuantity": 2, "Warehouse": "WH01" }
          ],
          "WorkerName": "Ramesh"
        }
        """;

    /// <summary>SAP returns null for U_ProdType / U_DwgNo on orders where the user fields were never filled.</summary>
    private const string PayloadWithNullProductionCategory = """
        {
          "ProductionOrder": {
            "AbsoluteEntry": 639,
            "ItemNo": "FG-1",
            "U_ProdType": null,
            "U_DwgNo": null
          },
          "ProductionOrderLinesEntryNumber": []
        }
        """;

    private const string PayloadWithProductionCategory = """
        {
          "ProductionOrder": {
            "AbsoluteEntry": 631,
            "ItemNo": "FG-1",
            "U_ProdType": "INT",
            "U_DwgNo": "THE"
          },
          "ProductionOrderLinesEntryNumber": []
        }
        """;

    [Test]
    public void Save_payload_without_production_category_is_valid()
    {
        var model = JsonSerializer.Deserialize<SapInventoryGenExitRequestOrderLines>(
            PayloadWithoutProductionCategory, WebOptions)!;

        var modelState = Validate(model);

        modelState.IsValid.Should().BeTrue(
            "ProductionCategory is an optional SAP user field, not something the browser supplies, but got {0}",
            Describe(modelState));
    }

    [Test]
    public void Save_payload_with_null_production_category_is_valid()
    {
        var model = JsonSerializer.Deserialize<SapInventoryGenExitRequestOrderLines>(
            PayloadWithNullProductionCategory, WebOptions)!;

        var modelState = Validate(model);

        modelState.IsValid.Should().BeTrue(
            "SAP returns null for user fields that were never filled in, but got {0}",
            Describe(modelState));
        modelState.Keys.Should().NotContain("ProductionOrder.ProductionCategory");
        modelState.Keys.Should().NotContain("ProductionOrder.DrawingNo");

        // An update must not write a blank over whatever SAP holds in the user field.
        var outgoing = JsonSerializer.Serialize(model.ProductionOrder);
        outgoing.Should().NotContain("U_ProdType");
        outgoing.Should().NotContain("U_DwgNo");
    }

    [Test]
    public void Save_payload_preserves_production_category_read_from_sap()
    {
        var model = JsonSerializer.Deserialize<SapInventoryGenExitRequestOrderLines>(
            PayloadWithProductionCategory, WebOptions)!;

        Validate(model).IsValid.Should().BeTrue();
        model.ProductionOrder.ProductionCategory.Should().Be("INT");
        model.ProductionOrder.DrawingNo.Should().Be("THE");

        // The same object is sent back to SAP, so the user fields must round-trip under their SAP names.
        var outgoing = JsonSerializer.Serialize(model.ProductionOrder);
        outgoing.Should().Contain("\"U_ProdType\":\"INT\"");
        outgoing.Should().Contain("\"U_DwgNo\":\"THE\"");
    }
}
