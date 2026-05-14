namespace DustAdvisor.Algorithm.Domain
{
    public sealed class AdvisorOptions
    {
        public Strategy Strategy { get; }
        public bool KeepStandardLegal { get; }

        public AdvisorOptions(Strategy strategy = Strategy.SafeOnly,
                              bool keepStandardLegal = true)
        {
            Strategy = strategy;
            KeepStandardLegal = keepStandardLegal;
        }
    }
}
