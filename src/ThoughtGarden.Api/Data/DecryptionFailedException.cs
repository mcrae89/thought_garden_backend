namespace ThoughtGarden.Api.Data
{
    public class DecryptionFailedException : Exception
    {
        public DecryptionFailedException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
