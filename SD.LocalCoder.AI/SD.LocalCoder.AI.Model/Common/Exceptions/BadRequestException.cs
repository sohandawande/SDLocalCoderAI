namespace SD.LocalCoder.AI.Model.Common.Exceptions
{
    public sealed class BadRequestException : BaseException
    {
        public BadRequestException(string message)
            : base(message)
        {
        }
    }
}
