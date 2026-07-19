using System.Text.Json.Serialization;

namespace SapApi.Domain.Entities
{
    public class ApprovalPolicyRule
    {
        public int Id { get; set; }

        public int ApprovalPolicyId { get; set; }

        // Back-reference to the parent — redundant since this entity is always accessed via
        // ApprovalPolicy.Rules, and EF's fixup would otherwise create a direct cycle.
        [JsonIgnore] public ApprovalPolicy Policy { get; set; }

        // Field name like "DocTotal"
        public string FieldName { get; set; } = string.Empty;

        // GreaterThan, Equal, etc
        public string Operator { get; set; } = string.Empty;

        // Store as string, convert dynamically
        public string Value { get; set; } = string.Empty;
    }
}