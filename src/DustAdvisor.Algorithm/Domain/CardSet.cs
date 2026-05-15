namespace DustAdvisor.Algorithm.Domain
{
    public sealed class CardSet
    {
        public string Code { get; }
        public bool IsCore => Code == "CORE";
        public bool IsStandardLegal { get; }

        public CardSet(string code, bool isStandardLegal)
        {
            Code = code;
            IsStandardLegal = isStandardLegal;
        }
    }
}
