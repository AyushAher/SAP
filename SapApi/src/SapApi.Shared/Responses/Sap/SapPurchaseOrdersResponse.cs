using SapApi.Shared.Requests;

namespace SapApi.Shared.Responses.Sap
{
    public record SapPurchaseOrdersResponse : SapBaseResponse
    {
        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocEntry { get; set; }
        
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocNum { get; set; }

        /// <summary>Document numbering series (NNM1). Required when the company uses per-BPL / per-FY series.</summary>
        [JsonPropertyName("Series"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Series { get; set; }

        [JsonPropertyName("DocType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocType { get; set; }

        [JsonPropertyName("Project"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Project { get; set; }

        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardCode { get; set; }

        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardName { get; set; }

        [JsonPropertyName("DocTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DocTotal { get; set; }

        [JsonPropertyName("VatSum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? VatSum { get; set; }
        [JsonPropertyName("NumAtCard"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NumAtCard { get; set; }


        [JsonPropertyName("DocumentStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocumentStatus { get; set; }

        [JsonPropertyName("DocCurrency"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocCurrency { get; set; }

        [JsonPropertyName("DocRate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DocRate { get; set; }

        [JsonPropertyName("JournalMemo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JournalMemo { get; set; }

        [JsonPropertyName("Comments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Comments { get; set; }

        [JsonPropertyName("DocTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TimeOnly? DocTime { get; set; }

        [JsonPropertyName("SalesPersonCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SalesPersonCode { get; set; }

        /// <summary>SAP OPOR.OwnerCode — employee who owns/approves the document.</summary>
        [JsonPropertyName("DocumentsOwner"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocumentsOwner { get; set; }

        [JsonPropertyName("TransportationCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TransportationCode { get; set; }

        [JsonPropertyName("DocumentLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapInventoryTransferItemsRequests>? DocumentLines { get; set; } = [];


        [JsonPropertyName("DocDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DocDate { get; set; }

        [JsonPropertyName("PostingDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? PostingDate { get; set; }

        [JsonPropertyName("DueDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DueDate { get; set; }
        [JsonPropertyName("DocDueDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DocDueDate { get; set; }
        [JsonPropertyName("TaxDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? TaxDate { get; set; }
        [JsonPropertyName("BPL_IDAssignedToInvoice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BPLId { get; set; } = 1;

        /// <summary>API alias so clients can read/write branch id as BPLId (SAP uses BPL_IDAssignedToInvoice).</summary>
        [JsonPropertyName("BPLId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BPLIdClient
        {
            get => BPLId;
            set => BPLId = value;
        }

        [JsonPropertyName("ContactPersonCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ContactPersonCode { get; set; }

        [JsonPropertyName("ShipToCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ShipToCode { get; set; }

        [JsonPropertyName("RoundingDiffAmount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? RoundingDiffAmount { get; set; }

        [JsonPropertyName("TotalDiscount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? TotalDiscount { get; set; }

        [JsonPropertyName("U_Stage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage { get; set; }

        [JsonPropertyName("U_Warehouse"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UWarehouse { get; set; }

        [JsonPropertyName("U_Owner"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UOwner { get; set; }

        /// <summary>PO type UDF — TN requires U_ProdNo on lines when value is JOB.</summary>
        [JsonPropertyName("U_PO_Type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UPoType { get; set; }

        /// <summary>Open order / transporter ref UDF — required when vendor BP Series = 124.</summary>
        [JsonPropertyName("U_TRN"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UTrn { get; set; }

        /// <summary>DRP dispatch id — required with U_DispachAdd when any line warehouse is DRP/DRP2.</summary>
        [JsonPropertyName("U_DisID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDisId { get; set; }

        /// <summary>Note: SAP UDF name is misspelled Dispach (not Dispatch).</summary>
        [JsonPropertyName("U_DispachAdd"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDispachAdd { get; set; }

        /// <summary>User remark — mandatory when cancelling a PO (TN).</summary>
        [JsonPropertyName("U_REMARK"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? URemark { get; set; }

        /// <summary>
        /// Dispatch To / Ship To BP (ADOC U_CardCode, linked to BusinessPartners).
        /// Prefer this over the legacy non-existent U_DispatchTo name.
        /// </summary>
        [JsonPropertyName("U_CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UCardCode { get; set; }

        /// <summary>Legacy alias — not a valid Service Layer property on this company DB.</summary>
        [JsonPropertyName("U_DispatchTo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDispatchTo { get; set; }

        [JsonPropertyName("U_ContactPerson"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UContactPerson { get; set; }

        [JsonPropertyName("U_PriceBasis"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UPriceBasis { get; set; }

        [JsonPropertyName("U_ModeOfTransport"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UModeOfTransport { get; set; }

        [JsonPropertyName("U_MatOutDoc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UMatOutDoc { get; set; }

        [JsonPropertyName("U_GoodsIssue"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UGoodsIssue { get; set; }

        [JsonPropertyName("U_MatInDoc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UMatInDoc { get; set; }

        [JsonPropertyName("U_GoodsReceipt"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UGoodsReceipt { get; set; }

        /// <summary>Delivery Terms (OPOR.U_DL).</summary>
        [JsonPropertyName("U_DL"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDelTerms { get; set; }

        /// <summary>Inspection By (OPOR.U_INSPBY).</summary>
        [JsonPropertyName("U_INSPBY"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UInspectionBy { get; set; }

        /// <summary>Transportation (OPOR.U_TRANS).</summary>
        [JsonPropertyName("U_TRANS"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UTransportation { get; set; }

        /// <summary>Supervision (OPOR.U_SUPR).</summary>
        [JsonPropertyName("U_SUPR"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? USupervision { get; set; }

        /// <summary>Transit Insurance (OPOR.U_TRANINSU).</summary>
        [JsonPropertyName("U_TRANINSU"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UTransitIns { get; set; }

        /// <summary>Drawings &amp; Documents (OPOR.U_DRA_DOC).</summary>
        [JsonPropertyName("U_DRA_DOC"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDrawDocs { get; set; }

        /// <summary>Loading (OPOR.U_LOAD).</summary>
        [JsonPropertyName("U_LOAD"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ULoading { get; set; }

        /// <summary>Warranty (OPOR.U_WARR).</summary>
        [JsonPropertyName("U_WARR"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UWarranty { get; set; }

        /// <summary>Unloading (OPOR.U_UN_LOAD).</summary>
        [JsonPropertyName("U_UN_LOAD"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UUnloading { get; set; }

        /// <summary>Any Other Remarks (OPOR.U_ANOTHREM).</summary>
        [JsonPropertyName("U_ANOTHREM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UOtherRemark { get; set; }

        /// <summary>Painting (OPOR.U_PAIN).</summary>
        [JsonPropertyName("U_PAIN"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UPainting { get; set; }

        /// <summary>Test Certificates (OPOR.U_TC).</summary>
        [JsonPropertyName("U_TC"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UTestCerts { get; set; }

        [JsonPropertyName("U_B1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic1 { get; set; }

        [JsonPropertyName("U_B2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic2 { get; set; }

        [JsonPropertyName("U_B3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic3 { get; set; }

        [JsonPropertyName("U_B4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic4 { get; set; }

        [JsonPropertyName("U_B5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic5 { get; set; }

        [JsonPropertyName("U_B6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic6 { get; set; }

        [JsonPropertyName("U_B7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic7 { get; set; }

        [JsonPropertyName("U_B8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic8 { get; set; }

        [JsonPropertyName("U_B9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic9 { get; set; }

        [JsonPropertyName("U_B10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic10 { get; set; }
        [JsonPropertyName("U_B11"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic11 { get; set; }

        [JsonPropertyName("U_G1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst1 { get; set; }

        [JsonPropertyName("U_G2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst2 { get; set; }

        [JsonPropertyName("U_G3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst3 { get; set; }

        [JsonPropertyName("U_G4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst4 { get; set; }

        [JsonPropertyName("U_G5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst5 { get; set; }

        [JsonPropertyName("U_G6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst6 { get; set; }

        [JsonPropertyName("U_G7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst7 { get; set; }

        [JsonPropertyName("U_G8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst8 { get; set; }

        [JsonPropertyName("U_G9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst9 { get; set; }

        [JsonPropertyName("U_G10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst10 { get; set; }

        [JsonPropertyName("U_G11"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst11 { get; set; }

        [JsonPropertyName("U_D1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes1 { get; set; }

        [JsonPropertyName("U_D2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes2 { get; set; }

        [JsonPropertyName("U_D3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes3 { get; set; }

        [JsonPropertyName("U_D4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes4 { get; set; }

        [JsonPropertyName("U_D5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes5 { get; set; }

        [JsonPropertyName("U_D6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes6 { get; set; }

        [JsonPropertyName("U_D7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes7 { get; set; }

        [JsonPropertyName("U_D8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes8 { get; set; }

        [JsonPropertyName("U_D9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes9 { get; set; }

        [JsonPropertyName("U_D10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes10 { get; set; }
        [JsonPropertyName("U_D11"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes11 { get; set; }


        [JsonPropertyName("U_S1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage1 { get; set; }

        [JsonPropertyName("U_S2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage2 { get; set; }

        [JsonPropertyName("U_S3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage3 { get; set; }

        [JsonPropertyName("U_S4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage4 { get; set; }

        [JsonPropertyName("U_S5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage5 { get; set; }

        [JsonPropertyName("U_S6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage6 { get; set; }

        [JsonPropertyName("U_S7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage7 { get; set; }

        [JsonPropertyName("U_S8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage8 { get; set; }

        [JsonPropertyName("U_S9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage9 { get; set; }

        [JsonPropertyName("U_S10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage10 { get; set; }
        [JsonPropertyName("U_S11"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage11 { get; set; }


        [JsonPropertyName("U_T1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType1 { get; set; }

        [JsonPropertyName("U_T2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType2 { get; set; }

        [JsonPropertyName("U_T3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType3 { get; set; }

        [JsonPropertyName("U_T4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType4 { get; set; }

        [JsonPropertyName("U_T5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType5 { get; set; }

        [JsonPropertyName("U_T6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType6 { get; set; }

        [JsonPropertyName("U_T7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType7 { get; set; }

        [JsonPropertyName("U_T8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType8 { get; set; }

        [JsonPropertyName("U_T9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType9 { get; set; }

        [JsonPropertyName("U_T10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType10 { get; set; }

        [JsonPropertyName("U_T11"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType11 { get; set; }

        public List<PaymentTermsUdf> CreateUdfList()
        {
            return
            [
                new PaymentTermsUdf
                {
                    Gst = UGst1,
                    Basic = UBasic1,
                    Desc = UDes1,
                    Stage = UStage1,
                    Type = UType1,
                    Id = 1
                },
                new PaymentTermsUdf
                {
                    Gst = UGst2,
                    Type = UType2,
                    Basic = UBasic2,
                    Stage = UStage2,
                    Desc = UDes2,
                    Id = 2
                },
                new PaymentTermsUdf
                {
                    Basic = UBasic3,
                    Stage = UStage3,
                    Gst = UGst3,
                    Type = UType3,
                    Desc = UDes3,
                    Id = 3
                },
                new PaymentTermsUdf
                {
                    Basic = UBasic4,
                    Stage = UStage4,
                    Type = UType4,
                    Gst = UGst4,
                    Desc = UDes4,
                    Id = 4
                },
                new PaymentTermsUdf
                {
                    Basic = UBasic5,
                    Stage = UStage5,
                    Gst = UGst5,
                    Type = UType5,
                    Desc = UDes5,
                    Id = 5
                },
                new PaymentTermsUdf
                {
                    Stage = UStage6,
                    Basic = UBasic6,
                    Type = UType6,
                    Gst = UGst6,
                    Desc = UDes6,
                    Id = 6
                },
                new PaymentTermsUdf
                {
                    Stage = UStage7,
                    Basic = UBasic7,
                    Type = UType7,
                    Gst = UGst7,
                    Desc = UDes7,
                    Id = 7
                },
                new PaymentTermsUdf
                {
                    Stage = UStage8,
                    Basic = UBasic8,
                    Type = UType8,
                    Gst = UGst8,
                    Desc = UDes8,
                    Id = 8
                },
                new PaymentTermsUdf
                {
                    Basic = UBasic9,
                    Gst = UGst9,
                    Stage = UStage9,
                    Type = UType9,
                    Desc = UDes9,
                    Id = 9
                },
                new PaymentTermsUdf
                {
                    Basic = UBasic10,
                    Stage = UStage10,
                    Type = UType10,
                    Gst = UGst10,
                    Desc = UDes10,
                    Id = 10
                },

                new PaymentTermsUdf
                {
                    Basic = UBasic11,
                    Stage = UStage11,
                    Type = UType11,
                    Gst = UGst11,
                    Desc = UDes11,
                    Id = 11
                }
            ];
        }

    }

    public record GetAllSapPurchaseOrdersResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapPurchaseOrdersResponse>? Value { get; set; }

    }

    public class PaymentTermsUdf
    {
        public int? Id { get; set; }
        public string? Desc { get; set; }
        public double? Gst { get; set; }
        public double? Basic { get; set; }
        public string? Stage { get; set; }
        public string? Type { get; set; }

        public string DropDownValue()
        {
            if (!string.IsNullOrEmpty(Desc))
                return Desc;
            return "Basic " + (Basic ?? 0) + "% & " + "GST " + (Gst ?? 0) + "%";
        }
    }

    public class SapWithholdingTaxDataCollectionResponse
    {
        [JsonPropertyName("WTCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WtCode { get; set; }
        [JsonPropertyName("BPCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BPCode { get; set; }
        [JsonPropertyName("TaxableAmount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? TaxableAmount { get; set; }
    }
}