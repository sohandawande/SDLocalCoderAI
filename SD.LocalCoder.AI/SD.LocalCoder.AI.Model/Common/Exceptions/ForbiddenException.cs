namespace SD.LocalCoder.AI.Model.Common.Exceptions
{
    public sealed class ForbiddenException : BaseException
    {
        public ForbiddenException(string message)
            : base(message)
        {
        }
    }
}
