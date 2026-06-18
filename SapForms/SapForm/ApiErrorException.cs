namespace SapForm
{
    /// <summary>
    /// Represents an exception that is thrown when an API operation fails due to a specific error condition.
    /// </summary>
    /// <remarks>Use this exception to capture and handle error responses from API calls that include an error code
    /// and message. The <see cref="ErrorCode"/> property provides additional information about the nature of the error,
    /// which can be used for logging or conditional error handling.</remarks>
    public class ApiErrorException : Exception
    {
        /// <summary>
        /// Gets or sets the error code that identifies the specific error condition encountered.
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the ApiErrorException class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that describes the reason for the exception.</param>
        public ApiErrorException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the ApiErrorException class with a specified error code and error message.
        /// </summary>
        /// <param name="errorCode">The error code that identifies the specific API error. Cannot be null.</param>
        /// <param name="message">The message that describes the error. Cannot be null.</param>
        public ApiErrorException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}