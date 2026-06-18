using SapApi.Modals;
using Microsoft.AspNetCore.Identity;

namespace SapApi
{
    public class ApiErrorException : Exception
    {
        public string ErrorCode { get; set; }

        /// <summary>
        /// To throw custom error exception
        /// </summary>
        /// <param name="errorCode"> Should be in <see cref="BaseErrorCodes"/>. </param>
        public ApiErrorException(string errorCode)
            : base(errorCode + ": Some Error Occurred. Please contact support team.")
        {

            ErrorCode = errorCode;
        }

        /// <summary>
        /// To throw custom error exception
        /// </summary>
        /// <param name="errorCode"> Should be in <see cref="BaseErrorCodes"/>. </param>
        /// <param name="message">Custom message</param>
        public ApiErrorException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// To throw custom error exception
        /// </summary>
        /// <param name="errorCode"> Should be in <see cref="BaseErrorCodes"/>. </param>
        /// <param name="innerException">rethrow an exception</param>
        public ApiErrorException(string errorCode, Exception innerException)
            : base(errorCode, innerException)
        {
            ErrorCode = errorCode;
        }

        public const string Forbidden = "Forbidden";

    }
}
