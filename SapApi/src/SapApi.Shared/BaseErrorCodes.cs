namespace SapApi.Shared
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

        #endregion

    }
}
