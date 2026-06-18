namespace SapApi.Shared.Requests
{
    public class SapQueries
    {
        public string? Filter { get; set; }
        public string? OrderBy { get; set; }
        public string? Select { get; set; }
        public string? Skip { get; set; }
        public string? Top { get; set; }
        public bool InlineCount { get; set; }

        public string GetQueryValue()
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(Filter))
                parts.Add("$filter=" + Uri.EscapeDataString(Filter));

            if (!string.IsNullOrEmpty(Select))
                parts.Add("$select=" + Uri.EscapeDataString(Select));

            if (!string.IsNullOrEmpty(OrderBy))
                parts.Add("$orderby=" + Uri.EscapeDataString(OrderBy));

            if (!string.IsNullOrEmpty(Skip))
                parts.Add("$skip=" + Uri.EscapeDataString(Skip));

            if (!string.IsNullOrEmpty(Top))
                parts.Add("$top=" + Uri.EscapeDataString(Top));

            if (InlineCount)
                parts.Add("$inlinecount=allpages");

            return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        }
    }
}
