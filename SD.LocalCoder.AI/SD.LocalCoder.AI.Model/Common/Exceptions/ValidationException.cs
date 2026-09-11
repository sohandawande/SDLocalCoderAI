namespace SD.LocalCoder.AI.Model.Common.Exceptions
{
    public sealed class ValidationException : BaseException
    {
        public IReadOnlyCollection<string> Errors { get; }

        public ValidationException(IEnumerable<string> errors)
            : base("One or more validation failures occurred.")
        {
            if (errors is null)
            {
                Errors = Array.Empty<string>();
            }
            else
            {
                Errors = errors.ToList().AsReadOnly();
            }
        }

        public ValidationException(string message)
            : base(message)
        {
            Errors = new[] { message };
        }
    }
}
