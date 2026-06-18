//using MongoDB.Bson;
using SapApi.Modals.Enums.Pagination;
using SapApi.Modals.Pagination;

namespace Shared.Modals.Pagination
{
    public partial class PaginationRequestModal<T> where T : class, new()
    {

        ///// <summary>
        ///// Builds a MongoDB filter query based on the current pagination settings, sorting criteria, and filters.
        ///// </summary>
        ///// <returns>A list of BsonDocument representing the filter query.</returns>
        //public List<BsonDocument> BuildFilterQuery()
        //{
        //    var skip = (CurrentPage - 1) * PageSize ?? 1; // Calculate skip value based on current page and page size
        //    var array = new List<BsonDocument>
        //    {
        //        new("$skip", skip), // Skip documents
        //        new("$limit", PageSize) // Limit number of documents
        //    };

        //    if (Sorts is { Count: > 0 })
        //    {
        //        array.Add(new BsonDocument("$sort", MongoSort())); // Add sorting criteria to the query
        //    }

        //    if (Filters is { Count: > 0 })
        //    {
        //        array.Add(new BsonDocument("$match", MongoFilters())); // Add filter conditions to the query
        //    }

        //    return array;
        //}

        ///// <summary>
        ///// Builds a MongoDB filter document based on the specified filter conditions.
        ///// </summary>
        ///// <returns>A BsonDocument representing the filter conditions.</returns>
        //private BsonDocument MongoFilters()
        //{
        //    if (Filters is null or { Count: 0 })
        //        return new BsonDocument(); // Return an empty document if no filters are specified

        //    var andFilterDocumentQueryList = new List<BsonDocument>();
        //    var orFilterDocumentQueryList = new List<BsonDocument>();
        //    foreach (var param in Filters)
        //    {
        //        // Create MongoDB filter based on filter type
        //        var documentQueryRaw = param.FilterType switch
        //        {
        //            FilterType.Equals => GetFilterTypeBasedOnDataType(param, param.MongoProperty),
        //            FilterType.LessThan => new BsonDocument(param.MongoProperty,
        //                GetFilterTypeBasedOnDataType(param, "$lt")),
        //            FilterType.Contains => new BsonDocument(param.MongoProperty,
        //                GetFilterTypeBasedOnDataType(param, "$regex")),
        //            FilterType.NotContains => new BsonDocument(param.MongoProperty,
        //                new BsonDocument("$not", GetFilterTypeBasedOnDataType(param, "$regex"))),
        //            FilterType.NotEquals => new BsonDocument(param.MongoProperty,
        //                new BsonDocument("$not", GetFilterTypeBasedOnDataType(param, "$eq"))),
        //            FilterType.GreaterThan => new BsonDocument(param.MongoProperty,
        //                GetFilterTypeBasedOnDataType(param, "$gt")),
        //            _ => throw new ArgumentOutOfRangeException()
        //        };

        //        // Add the filter to the appropriate filter list based on condition
        //        if (param.FilterCondition == FilterCondition.And)
        //            andFilterDocumentQueryList.Add(documentQueryRaw);
        //        else orFilterDocumentQueryList.Add(documentQueryRaw);
        //    }

        //    // Convert filter lists to BsonArray
        //    var andString = andFilterDocumentQueryList.Count > 0
        //        ? new BsonArray(andFilterDocumentQueryList.ToArray())
        //        : new BsonArray();

        //    var orString = orFilterDocumentQueryList.Count > 0
        //        ? new BsonArray(orFilterDocumentQueryList.ToArray())
        //        : new BsonArray();

        //    // Construct the final filter document
        //    var doc = new BsonArray();
        //    if (orFilterDocumentQueryList.Count > 0) doc.Add(new BsonDocument("$or", orString));
        //    if (andFilterDocumentQueryList.Count > 0) doc.Add(new BsonDocument("$and", andString));

        //    return new BsonDocument("$or", doc);
        //}

        ///// <summary>
        ///// Builds a MongoDB sort document based on the specified sorting criteria.
        ///// </summary>
        ///// <returns>A BsonDocument representing the sorting criteria.</returns>
        //private BsonDocument MongoSort()
        //{
        //    var document = new BsonDocument();
        //    if (Sorts is null or { Count: 0 })
        //        return document; // Return an empty document if no sorting criteria are specified

        //    foreach (var sort in Sorts)
        //    {
        //        // Add sorting criteria to the document based on sort type
        //        _ = sort.SortType switch
        //        {
        //            SortType.Asc => document.Add(sort.MongoProperty, 1),
        //            SortType.Desc => document.Add(sort.MongoProperty, -1),
        //            _ => throw new ArgumentOutOfRangeException()
        //        };
        //    }

        //    return document;
        //}

        ///// <summary>
        ///// Builds a MongoDB filter document based on the data type of the filter value.
        ///// </summary>
        ///// <param name="filter">The filter containing the filter value and its data type.</param>
        ///// <param name="filterKey">The MongoDB filter key.</param>
        ///// <returns>A BsonDocument representing the filter condition based on data type.</returns>
        //private BsonDocument GetFilterTypeBasedOnDataType(Filters<T> filter, string filterKey)
        //{
        //    return filter.GetTypeOfMongoProperty() switch
        //    {
        //        "string" => new BsonDocument(filterKey, $"{filter.Value}"),
        //        "DateTime" => new BsonDocument(filterKey, DateTime.Parse(filter.Value?.ToString())),
        //        _ => new BsonDocument(filterKey, filter.Value)
        //    };
        //}
    }
}