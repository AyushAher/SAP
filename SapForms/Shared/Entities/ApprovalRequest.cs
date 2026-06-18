using Shared.Enums;
using System.ComponentModel.DataAnnotations.Schema;
namespace Shared.Entities
{
    public class ApprovalRequest
    {
        public int Id { get; set; }

        public int PolicyId { get; set; }

        public ApprovalDocumentType DocumentType { get; set; }

        public int RequesterUserId { get; set; }

        public ApprovalAction Action { get; set; }

        public bool IsApproved { get; set; }

        public ApprovalStatus OverallStatus { get; set; }
            = ApprovalStatus.Pending;

        public string? RequestBody { get; set; }
        public string? SupportingData { get; set; }
        public string? FailureReason { get; set; }

        public string? SapResponseDocNum { get; set; }
        public string? SapResponseDocEntry { get; set; }

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public ApplicationUser RequesterUser { get; set; }

        public ApprovalPolicy Policy { get; set; }
        [NotMapped] public bool IsLastApproval { get; set; }
        public ICollection<UserApproval> UserApprovals { get; set; }
            = new List<UserApproval>();
    }
}