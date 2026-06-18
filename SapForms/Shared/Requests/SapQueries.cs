namespace Shared.Requests
{
    public class SapQueries
    {
        public string? Filter { get; set; }
        public string? OrderBy { get; set; }
        public string? Select { get; set; }
        public string? Skip { get; set; }
        public string? Top { get; set; }

        public string GetQueryValue()
        {
            var val = "";

            if (!string.IsNullOrEmpty(Filter))
            {
                val += "$filter=" + Filter;
            }
            if (!string.IsNullOrEmpty(Select))
            {
                if (!string.IsNullOrEmpty(val))
                {
                    val += "&";
                }
                val += "$select=" + Select;
            }
            if (!string.IsNullOrEmpty(OrderBy))
            {
                if (!string.IsNullOrEmpty(val))
                {
                    val += "&";
                }
                val += "$orderby=" + OrderBy;
            }
            if (!string.IsNullOrEmpty(Skip))
            {
                if (!string.IsNullOrEmpty(val))
                {
                    val += "&";
                }
                val += "$skip=" + Skip;
            }
            if (!string.IsNullOrEmpty(Top))
            {
                if (!string.IsNullOrEmpty(val))
                {
                    val += "&";
                }
                val += "$top=" + Top;
            }

            return "?" + val;
        }
    }
}
