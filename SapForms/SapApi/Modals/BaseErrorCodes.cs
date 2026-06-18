namespace SapApi.Modals
{
    public class BaseErrorCodes
    {
        #region System

        public static string SystemError = "SYS-01";
        public static string NullValue = "SYS-02";
        public static string RecordExists = "SYS-03";

        #endregion

        #region Auth

        public static string IncorrectCredentials = "AUTH-01";

        public static string InvalidJwtToken { get; internal set; }
        public static string UnAuthorized { get; internal set; }
        public static string PropertyNameInvalid { get; internal set; }

        #endregion

    }
}
