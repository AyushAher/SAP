using System.Reflection;
//using MongoDB.Entities;

namespace SapApi.Modals.Pagination;

/// <summary>
/// Class representing the property name and type information for a given class T.
/// </summary>
/// <typeparam name="T">The class type for which the property information is being fetched.</typeparam>
public class PropertyName<T> where T : class, new()
{

    private string _property = string.Empty;
    private string _requestProperty = string.Empty;


    ///// <summary>
    ///// Gets the type of the MongoDB property in a friendly name format.
    ///// </summary>
    ///// <returns>A string representing the type of the MongoDB property.</returns>
    //public string GetTypeOfMongoProperty()
    //{
    //    var propertyChain = MongoProperty.Split('.');
    //    var currentType = typeof(T);

    //    foreach (var property in propertyChain)
    //    {
    //        var propertyInfo = Array.Find(currentType?.GetProperties() ?? [], x =>
    //                                   string.Equals(x.Name, property, StringComparison.CurrentCultureIgnoreCase))
    //                           ?? throw new ApiErrorException(BaseErrorCodes.PropertyNameInvalid,
    //                               $"Property {property} does not exists in the given type.");

    //        // If it's an array, get the element type
    //        if (propertyInfo.PropertyType.IsArray)
    //            currentType = propertyInfo.PropertyType.GetElementType();
    //        else if (propertyInfo.PropertyType.IsGenericType &&
    //                 propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
    //            currentType = propertyInfo.PropertyType.GetGenericArguments()[0];

    //        else currentType = propertyInfo.PropertyType;
    //    }

    //    return GetFriendlyTypeName(currentType);
    //}


    /// <summary>
    /// Converts a Type to a friendly name.
    /// </summary>
    /// <param name="type">The type to convert.</param>
    /// <returns>A string representing the friendly name of the type.</returns>
    private static string GetFriendlyTypeName(Type? type)
    {
        if (type == null) return string.Empty;

        if (type == typeof(string))
            return "string";
        if (type == typeof(DateTime?) || type == typeof(DateTime))
            return "DateTime";
        return type.Name;
    }

    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    /// <exception cref="ApiErrorException">Thrown when the property does not exist in the given type.</exception>
    public string Property
    {
        get => _property;
        set
        {
            _requestProperty = value;
            PropertyInfo[] props = typeof(T).GetProperties();
            PropertyInfo property = props.FirstOrDefault(propertyInfo =>
                               string.Equals(propertyInfo.Name, _requestProperty.Split(".")[0],
                                   StringComparison.CurrentCultureIgnoreCase))
                           ?? throw new ApiErrorException(BaseErrorCodes.PropertyNameInvalid,
                               $"Property {value} does not exists in the given type.");

            _property = property.Name;
        }
    }


    ///// <summary>
    ///// Gets the MongoDB property name, taking into account nested properties.
    ///// </summary>
    //public string MongoProperty
    //{
    //    get
    //    {
    //        if (_requestProperty.Split(".").Length > 1)
    //        {
    //            return _requestProperty;
    //        }

    //        var getFieldAttribute = typeof(T).GetProperty(Property)?.GetCustomAttribute<FieldAttribute>();
    //        return getFieldAttribute?.ElementName ?? Property;
    //    }
    //}


    /// <summary>
    /// Gets the column name for SQL queries.
    /// </summary>
    /// <param name="isCaseSensitive">Indicates if the column name should be case sensitive.</param>
    /// <returns>The column name for SQL queries.</returns>
    public string ColumnName(bool isCaseSensitive)
    {
        var columnAttribute = typeof(T).GetProperty(Property)?
            .GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?
            .Name ?? Property;

        return isCaseSensitive ? $"\"{columnAttribute}\"" : columnAttribute;
    }
}