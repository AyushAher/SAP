using System.ComponentModel;

namespace SapApi.Shared.Enums
{
    public enum ApprovalDocumentType
    {
        [Description("None")]
        None,
        [Description("Purchase Order")]
        PurchaseOrder,
        [Description("Production Order")]
        ProductionOrder,
        [Description("Stagewise Payments Down Payment")]
        StagewisePayments_DP,
        [Description("Outgoing Payments")]
        Payments,
        [Description("Inventory Items Transfer")]
        InventoryItemsTransfer,
        [Description("Issue For Production")]
        IssueForProduction,
    }
}