namespace SapForm.Services.Helpers
{
    public interface IHttpRequestHandler
    {
        /// <summary>
        /// Asynchronously retrieves and deserializes the resource at the specified URL as an object of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to which the response content will be deserialized. Must be compatible with the response format.</typeparam>
        /// <param name="url">The URL of the resource to retrieve. Cannot be null or empty.</param>
        /// <param name="setTimeout">Specify if needs to set the timeout or not.</param>
        /// <param name="checkCache">Specify if needs to check data exists in cache or directly execute the endpoint.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized object of type
        /// <typeparamref name="T"/>, or <see langword="null"/> if the resource is not found or cannot be deserialized.</returns>
        Task<T?> GetAsync<T>(string url, bool setTimeout = false, bool checkCache = true);

        /// <summary>
        /// Sends an HTTP POST request to the specified URL with the provided data and asynchronously returns the
        /// deserialized response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data to send in the POST request body.</typeparam>
        /// <typeparam name="TResponse">The type to which the response content will be deserialized.</typeparam>
        /// <param name="url">The URL to which the POST request is sent. Must be a valid absolute or relative URI.</param>
        /// <param name="data">The data to include in the POST request body. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized response of
        /// type TResponse, or null if the response body is empty.</returns>
        Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest? data);

        /// <summary>
        /// Sends an HTTP PATCH request to the specified URL with the provided data and asynchronously returns the
        /// deserialized response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request payload to be sent in the PATCH operation.</typeparam>
        /// <typeparam name="TResponse">The type of the response object expected from the PATCH operation.</typeparam>
        /// <param name="url">The endpoint URL to which the PATCH request is sent. Must be a valid, non-empty URI.</param>
        /// <param name="data">The data to be serialized and included in the PATCH request body.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized response object
        /// of type TResponse, or null if the response body is empty.</returns>
        Task<TResponse?> PatchAsync<TRequest, TResponse>(string url, TRequest data);

        /// <summary>
        /// Sends an HTTP PUT request to the specified URL with the provided data and asynchronously returns the
        /// deserialized response.
        /// </summary>
        Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest data);

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="docEntry"></param>
        /// <param name="idProperty"></param>
        /// <returns></returns>
        public Task PatchCachedEntityAsync<T>(string entity, int docEntry, string idProperty = "DocEntry");
    
        /// <summary>
        /// Executes a predefined SQL query against the SAP database with the specified parameters.
        /// </summary>
        /// <param name="queryName">Name of the SQL query which is created in the SAP Service Layer</param>
        /// <param name="parameters">Key Value pairs for the SQL parameters</param>
        /// <returns></returns>
        public Task<T?> ExecuteSqlQueryAsync<T>(string queryName, Dictionary<string, object> parameters);
    
    }
}