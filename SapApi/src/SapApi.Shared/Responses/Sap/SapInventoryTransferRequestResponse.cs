using SapApi.Shared.Requests;

namespace SapApi.Shared.Responses.Sap
{
    public class SapInventoryTransferRequestsRequest
    {
        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? DocEntry { get; set; }
        [JsonPropertyName("StockTransferLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapInventoryTransferItemsRequests>? StockTransferLines { get; set; } = [];
        [JsonPropertyName("Series"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Series { get; set; }
        [JsonPropertyName("Printed"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Printed { get; set; }
        [JsonPropertyName("DocDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? DocDate { get; set; }
        [JsonPropertyName("DueDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? DueDate { get; set; }
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardCode { get; set; }
        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardName { get; set; }
        [JsonPropertyName("Address"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Address { get; set; }
        [JsonPropertyName("Reference1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference1 { get; set; }
        [JsonPropertyName("Reference2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference2 { get; set; }
        [JsonPropertyName("Comments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Comments { get; set; }
        [JsonPropertyName("JournalMemo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? JournalMemo { get; set; }
        [JsonPropertyName("PriceList"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PriceList { get; set; }
        [JsonPropertyName("SalesPersonCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? SalesPersonCode { get; set; }
        [JsonPropertyName("FromWarehouse"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FromWarehouse { get; set; }
        [JsonPropertyName("ToWarehouse"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ToWarehouse { get; set; }
        [JsonPropertyName("CreationDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? CreationDate { get; set; }
        [JsonPropertyName("UpdateDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? UpdateDate { get; set; }
        [JsonPropertyName("FinancialPeriod"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FinancialPeriod { get; set; }
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? DocNum { get; set; }
        [JsonPropertyName("TaxDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? TaxDate { get; set; }
        [JsonPropertyName("ContactPerson"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? ContactPerson { get; set; }
        [JsonPropertyName("FolioPrefixString"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FolioPrefixString { get; set; }
        [JsonPropertyName("DocObjectCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DocObjectCode { get; set; }
        [JsonPropertyName("AuthorizationStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AuthorizationStatus { get; set; }
        [JsonPropertyName("BPLName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BPLName { get; set; }
        [JsonPropertyName("VATRegNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VATRegNum { get; set; }
        [JsonPropertyName("AuthorizationCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AuthorizationCode { get; set; }
        [JsonPropertyName("StartDeliveryTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StartDeliveryTime { get; set; }
        [JsonPropertyName("EndDeliveryTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EndDeliveryTime { get; set; }
        [JsonPropertyName("VehiclePlate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VehiclePlate { get; set; }
        [JsonPropertyName("ATDocumentType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ATDocumentType { get; set; }
        [JsonPropertyName("EDocExportFormat"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EDocExportFormat { get; set; }
        [JsonPropertyName("ElecCommStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ElecCommStatus { get; set; }
        [JsonPropertyName("ElecCommMessage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ElecCommMessage { get; set; }
        [JsonPropertyName("PointOfIssueCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PointOfIssueCode { get; set; }
        [JsonPropertyName("Letter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Letter { get; set; }
        [JsonPropertyName("DocumentStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DocumentStatus { get; set; }
        [JsonPropertyName("ShipToCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ShipToCode { get; set; }
        [JsonPropertyName("SAPPassport"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SAPPassport { get; set; }
        [JsonPropertyName("DutyStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DutyStatus { get; set; }
        [JsonPropertyName("TransNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? TransNum { get; set; }
        [JsonPropertyName("BPLID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? BPLID { get; set; }
        [JsonPropertyName("FolioNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FolioNumber { get; set; }
        [JsonPropertyName("FolioNumberFrom"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FolioNumberFrom { get; set; }
        [JsonPropertyName("StartDeliveryDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? StartDeliveryDate { get; set; }
        [JsonPropertyName("EndDeliveryDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? EndDeliveryDate { get; set; }
        [JsonPropertyName("FolioNumberTo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FolioNumberTo { get; set; }
        [JsonPropertyName("AttachmentEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? AttachmentEntry { get; set; }
        [JsonPropertyName("LastPageFolioNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? LastPageFolioNumber { get; set; }

    }
    public record SapInventoryTransferRequestResponse : SapBaseResponse
    {

        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? DocEntry { get; set; }
        [JsonPropertyName("StockTransferLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapInventoryTransferItemsRequests>? StockTransferLines { get; set; } = [];
        [JsonPropertyName("Series"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Series { get; set; }
        [JsonPropertyName("Printed"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Printed { get; set; }
        [JsonPropertyName("DocDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? DocDate { get; set; }
        [JsonPropertyName("DueDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? DueDate { get; set; }
        public string? DocDateFormatted => DocDate?.ToString(Constants.DateTimeFormat);
        public string? DueDateFormatted => DueDate?.ToString(Constants.DateTimeFormat);
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardCode { get; set; }
        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardName { get; set; }
        [JsonPropertyName("Address"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Address { get; set; }
        [JsonPropertyName("Reference1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference1 { get; set; }
        [JsonPropertyName("Reference2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference2 { get; set; }
        [JsonPropertyName("Comments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Comments { get; set; }
        [JsonPropertyName("JournalMemo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? JournalMemo { get; set; }
        [JsonPropertyName("PriceList"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PriceList { get; set; }
        [JsonPropertyName("SalesPersonCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? SalesPersonCode { get; set; }
        [JsonPropertyName("FromWarehouse"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FromWarehouse { get; set; }
        [JsonPropertyName("ToWarehouse"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ToWarehouse { get; set; }
        [JsonPropertyName("CreationDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? CreationDate { get; set; }
        [JsonPropertyName("UpdateDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? UpdateDate { get; set; }
        [JsonPropertyName("FinancialPeriod"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FinancialPeriod { get; set; }
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? DocNum { get; set; }
        [JsonPropertyName("TaxDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? TaxDate { get; set; }
        [JsonPropertyName("ContactPerson"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? ContactPerson { get; set; }
        [JsonPropertyName("FolioPrefixString"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FolioPrefixString { get; set; }
        [JsonPropertyName("DocObjectCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DocObjectCode { get; set; }
        [JsonPropertyName("AuthorizationStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AuthorizationStatus { get; set; }
        [JsonPropertyName("BPLName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BPLName { get; set; }
        [JsonPropertyName("VATRegNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VATRegNum { get; set; }
        [JsonPropertyName("AuthorizationCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AuthorizationCode { get; set; }
        [JsonPropertyName("StartDeliveryTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StartDeliveryTime { get; set; }
        [JsonPropertyName("EndDeliveryTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EndDeliveryTime { get; set; }
        [JsonPropertyName("VehiclePlate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VehiclePlate { get; set; }
        [JsonPropertyName("ATDocumentType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ATDocumentType { get; set; }
        [JsonPropertyName("EDocExportFormat"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EDocExportFormat { get; set; }
        [JsonPropertyName("ElecCommStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ElecCommStatus { get; set; }
        [JsonPropertyName("ElecCommMessage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ElecCommMessage { get; set; }
        [JsonPropertyName("PointOfIssueCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PointOfIssueCode { get; set; }
        [JsonPropertyName("Letter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Letter { get; set; }
        [JsonPropertyName("DocumentStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DocumentStatus { get; set; }
        [JsonPropertyName("ShipToCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ShipToCode { get; set; }
        [JsonPropertyName("SAPPassport"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SAPPassport { get; set; }
        [JsonPropertyName("DutyStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DutyStatus { get; set; }
        [JsonPropertyName("TransNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? TransNum { get; set; }
        [JsonPropertyName("BPLID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? BPLID { get; set; }
        [JsonPropertyName("FolioNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FolioNumber { get; set; }
        [JsonPropertyName("FolioNumberFrom"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FolioNumberFrom { get; set; }
        [JsonPropertyName("StartDeliveryDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? StartDeliveryDate { get; set; }
        [JsonPropertyName("EndDeliveryDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? EndDeliveryDate { get; set; }
        [JsonPropertyName("FolioNumberTo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FolioNumberTo { get; set; }
        [JsonPropertyName("AttachmentEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? AttachmentEntry { get; set; }
        [JsonPropertyName("LastPageFolioNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? LastPageFolioNumber { get; set; }


        public SapInventoryTransferRequestsRequest GetRequestClass()
        {
            return new SapInventoryTransferRequestsRequest
            {
                DocEntry = DocEntry,
                StockTransferLines = StockTransferLines,
                Series = Series,
                Printed = Printed,
                DocDate = DocDate,
                DueDate = DueDate,
                CardCode = CardCode,
                CardName = CardName,
                Address = Address,
                Reference1 = Reference1,
                Reference2 = Reference2,
                Comments = Comments,
                JournalMemo = JournalMemo,
                PriceList = PriceList,
                SalesPersonCode = SalesPersonCode,
                FromWarehouse = FromWarehouse,
                ToWarehouse = ToWarehouse,
                CreationDate = CreationDate,
                UpdateDate = UpdateDate,
                FinancialPeriod = FinancialPeriod,
                DocNum = DocNum,
                TaxDate = TaxDate,
                ContactPerson = ContactPerson,
                FolioPrefixString = FolioPrefixString,
                DocObjectCode = DocObjectCode,
                AuthorizationStatus = AuthorizationStatus,
                BPLName = BPLName,
                VATRegNum = VATRegNum,
                AuthorizationCode = AuthorizationCode,
                StartDeliveryTime = StartDeliveryTime,
                EndDeliveryTime = EndDeliveryTime,
                VehiclePlate = VehiclePlate,
                ATDocumentType = ATDocumentType,
                EDocExportFormat = EDocExportFormat,
                ElecCommStatus = ElecCommStatus,
                ElecCommMessage = ElecCommMessage,
                PointOfIssueCode = PointOfIssueCode,
                Letter = Letter,
                DocumentStatus = DocumentStatus,
                ShipToCode = ShipToCode,
                SAPPassport = SAPPassport,
                DutyStatus = DutyStatus,
                TransNum = TransNum,
                BPLID = BPLID,
                FolioNumber = FolioNumber,
                FolioNumberFrom = FolioNumberFrom,
                StartDeliveryDate = StartDeliveryDate,
                EndDeliveryDate = EndDeliveryDate,
                FolioNumberTo = FolioNumberTo,
                AttachmentEntry = AttachmentEntry,
                LastPageFolioNumber = LastPageFolioNumber
            };
        }

    }

    public record SapInventoryTransferRequestListResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapInventoryTransferRequestResponse>? Value { get; set; }
    }
}