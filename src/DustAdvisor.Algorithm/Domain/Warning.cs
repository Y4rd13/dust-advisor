namespace DustAdvisor.Algorithm.Domain
{
    public sealed class Warning
    {
        public string CardId { get; }
        public string Message { get; }

        public Warning(string cardId, string message)
        {
            CardId = cardId;
            Message = message;
        }
    }
}
