using Shared.Enums;

namespace Shared.Entities
{
    public class ApprovalPolicyRule
    {
        public int Id { get; set; }

        public int ApprovalPolicyId { get; set; }
        public ApprovalPolicy Policy { get; set; }

        // Field name like "DocTotal"
        public string FieldName { get; set; } = string.Empty;

        // GreaterThan, Equal, etc
        public string Operator { get; set; } = string.Empty;

        // Store as string, convert dynamically
        public string Value { get; set; } = string.Empty;
    }
}