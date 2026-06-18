namespace SapApi.Shared.Exceptions;

public class ApiErrorException : Exception
{
    public string ErrorCode { get; }

    public ApiErrorException(string message) : base(message)
    {
        ErrorCode = "SYS-01";
    }

    public ApiErrorException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
