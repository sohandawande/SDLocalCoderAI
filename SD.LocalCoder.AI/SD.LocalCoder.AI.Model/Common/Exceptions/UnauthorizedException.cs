namespace SD.LocalCoder.AI.Model.Common.Exceptions
{
    public sealed class UnauthorizedException : BaseException
    {
        public UnauthorizedException(string message)
            : base(message)
        {
        }
    }
}
