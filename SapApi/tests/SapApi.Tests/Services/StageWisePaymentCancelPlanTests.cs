using FluentAssertions;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services;

public class StageWisePaymentCancelPlanTests
{
    [Test]
    public void BatchDownPayment_cancels_by_doc_entry_not_matching_outgoing_doc_num()
    {
        var plan = StageWisePaymentCancelPlanner.Build(new StageWisePayment
        {
            StageDesc = StageWisePaymentCancelPlanner.BatchDownPaymentStage,
            DownPaymentDocEntry = "9001",
            ApDownPaymentInvoiceEntryNumber = "123",
        });

        plan.DownPaymentDocEntries.Should().Equal("9001");
        plan.VendorPaymentDocEntries.Should().BeEmpty();
        plan.VendorPaymentDocNums.Should().BeEmpty();
        plan.DownPaymentDocNums.Should().BeEmpty();
    }

    [Test]
    public void BatchDownPayment_with_outgoing_on_same_row_cancels_each_by_doc_entry()
    {
        var plan = StageWisePaymentCancelPlanner.Build(new StageWisePayment
        {
            StageDesc = StageWisePaymentCancelPlanner.BatchDownPaymentStage,
            DownPaymentDocEntry = "9001",
            PaymentDocEntry = "8001",
            ApDownPaymentInvoiceEntryNumber = "123,123",
        });

        plan.DownPaymentDocEntries.Should().Equal("9001");
        plan.VendorPaymentDocEntries.Should().Equal("8001");
        plan.VendorPaymentDocNums.Should().BeEmpty();
        plan.DownPaymentDocNums.Should().BeEmpty();
    }

    [Test]
    public void BatchApPayment_cancels_outgoing_by_doc_entry_only()
    {
        var plan = StageWisePaymentCancelPlanner.Build(new StageWisePayment
        {
            StageDesc = StageWisePaymentCancelPlanner.BatchApPaymentStage,
            PaymentDocEntry = "8001",
            ApDownPaymentInvoiceEntryNumber = "123",
        });

        plan.VendorPaymentDocEntries.Should().Equal("8001");
        plan.DownPaymentDocEntries.Should().BeEmpty();
        plan.VendorPaymentDocNums.Should().BeEmpty();
        plan.DownPaymentDocNums.Should().BeEmpty();
    }

    [Test]
    public void Legacy_down_payment_doc_num_does_not_target_vendor_payment()
    {
        var plan = StageWisePaymentCancelPlanner.Build(new StageWisePayment
        {
            StageDesc = StageWisePaymentCancelPlanner.BatchDownPaymentStage,
            ApDownPaymentInvoiceEntryNumber = "123",
        });

        plan.DownPaymentDocNums.Should().Equal("123");
        plan.VendorPaymentDocNums.Should().BeEmpty();
        plan.VendorPaymentDocEntries.Should().BeEmpty();
    }

    [Test]
    public void Legacy_appended_outgoing_doc_num_is_last_only()
    {
        var plan = StageWisePaymentCancelPlanner.Build(new StageWisePayment
        {
            ApDownPaymentInvoiceEntryNumber = "123,456",
        });

        plan.DownPaymentDocNums.Should().Equal("123");
        plan.VendorPaymentDocNums.Should().Equal("456");
    }

    [Test]
    public void Legacy_batch_ap_doc_num_targets_vendor_payment_only()
    {
        var plan = StageWisePaymentCancelPlanner.Build(new StageWisePayment
        {
            StageDesc = StageWisePaymentCancelPlanner.BatchApPaymentStage,
            ApDownPaymentInvoiceEntryNumber = "123",
        });

        plan.VendorPaymentDocNums.Should().Equal("123");
        plan.DownPaymentDocNums.Should().BeEmpty();
    }

    [Test]
    public void Outgoing_with_same_doc_num_is_not_linked_unless_applied_to_this_down_payment()
    {
        var record = new StageWisePayment
        {
            DownPaymentDocEntry = "9001",
            ApDownPaymentInvoiceEntryNumber = "123",
        };
        var unrelatedOutgoing = new SapVendorPaymentsResponse
        {
            DocEntry = 7001,
            DocNumber = 123,
            PaymentInvoices =
            [
                new PaymentInvoice
                {
                    DocEntry = 8888,
                    InvoiceType = Constants.SapVendorPaymentInvoiceType.DownPayment,
                    SumApplied = 10,
                },
            ],
        };

        StageWisePaymentCancelPlanner.IsOutgoingPaymentLinkedToRecord(record, unrelatedOutgoing)
            .Should().BeFalse();
    }

    [Test]
    public void Outgoing_applied_to_this_down_payment_is_linked()
    {
        var record = new StageWisePayment { DownPaymentDocEntry = "9001" };
        var outgoing = new SapVendorPaymentsResponse
        {
            DocEntry = 8001,
            DocNumber = 123,
            PaymentInvoices =
            [
                new PaymentInvoice
                {
                    DocEntry = 9001,
                    InvoiceType = Constants.SapVendorPaymentInvoiceType.DownPayment,
                    SumApplied = 10,
                },
            ],
        };

        StageWisePaymentCancelPlanner.IsOutgoingPaymentLinkedToRecord(record, outgoing)
            .Should().BeTrue();
    }

    [Test]
    public void Outgoing_applied_to_this_ap_invoice_is_linked()
    {
        var record = new StageWisePayment { ApInvoiceDocEntry = "5507,5510" };
        var outgoing = new SapVendorPaymentsResponse
        {
            DocEntry = 8002,
            PaymentInvoices =
            [
                new PaymentInvoice
                {
                    DocEntry = 5510,
                    InvoiceType = Constants.SapVendorPaymentInvoiceType.Invoice,
                    SumApplied = 10,
                },
            ],
        };

        StageWisePaymentCancelPlanner.IsOutgoingPaymentLinkedToRecord(record, outgoing)
            .Should().BeTrue();
    }

    [Test]
    public void Stored_outgoing_doc_entry_is_trusted_only_when_sap_omits_invoice_lines()
    {
        var record = new StageWisePayment { PaymentDocEntry = "8001", DownPaymentDocEntry = "9001" };
        var outgoing = new SapVendorPaymentsResponse { DocEntry = 8001 };

        StageWisePaymentCancelPlanner.IsOutgoingPaymentLinkedToRecord(record, outgoing)
            .Should().BeTrue();

        StageWisePaymentCancelPlanner.IsOutgoingPaymentLinkedToRecord(
                record,
                new SapVendorPaymentsResponse { DocEntry = 8002 })
            .Should().BeFalse();
    }
}
