namespace SD.LocalCoder.AI.Model.Common.Exceptions
{
    public abstract class BaseException : Exception
    {
        protected BaseException(string message)
            : base(message)
        {
        }

        protected BaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
