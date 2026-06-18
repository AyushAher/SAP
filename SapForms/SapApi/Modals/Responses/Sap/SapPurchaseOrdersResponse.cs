using SapApi.Modals.Requests;

namespace SapApi.Modals.Responses.Sap
{
    public record SapPurchaseOrdersResponse : SapBaseResponse
    {
        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocEntry { get; set; }

        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardCode { get; set; }

        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardName { get; set; }

        [JsonPropertyName("DocTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DocTotal { get; set; }

        [JsonPropertyName("VatSum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? VatSum { get; set; }

        [JsonPropertyName("DocCurrency"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocCurrency { get; set; }

        [JsonPropertyName("DocRate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DocRate { get; set; }

        [JsonPropertyName("JournalMemo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JournalMemo { get; set; }

        [JsonPropertyName("DocTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TimeOnly? DocTime { get; set; }

        [JsonPropertyName("SalesPersonCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SalesPersonCode { get; set; }

        [JsonPropertyName("TransportationCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TransportationCode { get; set; }

        [JsonPropertyName("DocumentLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapInventoryTransferItemsRequests>? DocumentLines { get; set; } = [];

        [JsonPropertyName("DocDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DocDate { get; set; }

        [JsonPropertyName("DueDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DueDate { get; set; }



        [JsonPropertyName("U_Basic1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic1 { get; set; }

        [JsonPropertyName("U_Basic2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic2 { get; set; }

        [JsonPropertyName("U_Basic3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic3 { get; set; }

        [JsonPropertyName("U_Basic4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic4 { get; set; }

        [JsonPropertyName("U_Basic5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic5 { get; set; }

        [JsonPropertyName("U_Basic6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic6 { get; set; }

        [JsonPropertyName("U_Basic7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic7 { get; set; }

        [JsonPropertyName("U_Basic8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic8 { get; set; }

        [JsonPropertyName("U_Basic9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic9 { get; set; }

        [JsonPropertyName("U_Basic10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UBasic10 { get; set; }

        [JsonPropertyName("U_Gst1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst1 { get; set; }

        [JsonPropertyName("U_Gst2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst2 { get; set; }

        [JsonPropertyName("U_Gst3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst3 { get; set; }

        [JsonPropertyName("U_Gst4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst4 { get; set; }

        [JsonPropertyName("U_Gst5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst5 { get; set; }

        [JsonPropertyName("U_Gst6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst6 { get; set; }

        [JsonPropertyName("U_Gst7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst7 { get; set; }

        [JsonPropertyName("U_Gst8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst8 { get; set; }

        [JsonPropertyName("U_Gst9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst9 { get; set; }

        [JsonPropertyName("U_Gst10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UGst10 { get; set; }

        [JsonPropertyName("U_Des1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes1 { get; set; }

        [JsonPropertyName("U_Des2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes2 { get; set; }

        [JsonPropertyName("U_Des3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes3 { get; set; }

        [JsonPropertyName("U_Des4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes4 { get; set; }

        [JsonPropertyName("U_Des5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes5 { get; set; }

        [JsonPropertyName("U_Des6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes6 { get; set; }

        [JsonPropertyName("U_Des7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes7 { get; set; }

        [JsonPropertyName("U_Des8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes8 { get; set; }

        [JsonPropertyName("U_Des9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes9 { get; set; }

        [JsonPropertyName("U_Des10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UDes10 { get; set; }


        [JsonPropertyName("U_Stage1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage1 { get; set; }

        [JsonPropertyName("U_Stage2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage2 { get; set; }

        [JsonPropertyName("U_Stage3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage3 { get; set; }

        [JsonPropertyName("U_Stage4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage4 { get; set; }

        [JsonPropertyName("U_Stage5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage5 { get; set; }

        [JsonPropertyName("U_Stage6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage6 { get; set; }

        [JsonPropertyName("U_Stage7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage7 { get; set; }

        [JsonPropertyName("U_Stage8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage8 { get; set; }

        [JsonPropertyName("U_Stage9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage9 { get; set; }

        [JsonPropertyName("U_Stage10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UStage10 { get; set; }


        [JsonPropertyName("U_Type1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType1 { get; set; }

        [JsonPropertyName("U_Type2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType2 { get; set; }

        [JsonPropertyName("U_Type3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType3 { get; set; }

        [JsonPropertyName("U_Type4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType4 { get; set; }

        [JsonPropertyName("U_Type5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType5 { get; set; }

        [JsonPropertyName("U_Type6"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType6 { get; set; }

        [JsonPropertyName("U_Type7"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType7 { get; set; }

        [JsonPropertyName("U_Type8"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType8 { get; set; }

        [JsonPropertyName("U_Type9"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType9 { get; set; }

        [JsonPropertyName("U_Type10"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UType10 { get; set; }

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
}