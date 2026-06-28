using Newtonsoft.Json;

namespace SapApi.Modals
{
    public class ApiResponseModal
    {
        [JsonProperty("errorCode")] public string? ErrorCode { get; internal set; }

        [JsonProperty("message")] public string? Message { get; internal set; }

        [JsonProperty("succeeded")] public bool Succeeded { get; internal set; }

        protected ApiResponseModal(bool succeeded, string? errorCode = null, string? message = null)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            Message = message;
            if (errorCode == null || !string.IsNullOrEmpty(message)) return;
            //new BaseErrorCodes().ErrorMessages.TryGetValue(errorCode, out message);
            //Message = message;
        }

        /// <summary>
        /// To return a success response
        /// </summary>
        /// <returns></returns>
        public static ApiResponseModal Success()
            => new(true);
    }

    public sealed class ApiResponseModal<T> : ApiResponseModal
    {
        [JsonProperty("data")] public T? Data { get; private set; }

        private ApiResponseModal(bool succeeded, string? errorCode = null, string? message = null)
            : base(succeeded, errorCode, message)
        {
            Data = default;
        }

        private ApiResponseModal(T data, bool succeeded, string? errorCode = null)
            : base(succeeded, errorCode)
        {
            Data = data;
        }

        /// <summary>
        /// Send a Success response along with data.
        /// </summary>
        /// <param name="data">Response data.</param>
        /// <returns></returns>
        public static ApiResponseModal<T> Success(T data)
            => new(data, true);


        /// <summary>
        /// When there was an unhandled exception
        /// </summary>
        /// <param name="exception">Exception occurred.</param>
        /// <returns></returns>
        public static ApiResponseModal<T> Fatal(Exception exception)
        {
            Log.Error(exception, BaseErrorCodes.SystemError);
            return new ApiResponseModal<T>(false, BaseErrorCodes.SystemError);
        }


        /// <summary>
        /// When there was an unhandled exception
        /// </summary>
        /// <param name="exception">Exception occurred.</param>
        /// <returns></returns>
        public static ApiResponseModal<T> Fatal(ApiErrorException exception)
        {
            Log.Error(exception, exception.ErrorCode);
            return new ApiResponseModal<T>(false, exception.ErrorCode, exception.Message);
        }

    }
}