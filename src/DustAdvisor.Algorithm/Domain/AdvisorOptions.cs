namespace DustAdvisor.Algorithm.Domain
{
    public sealed class AdvisorOptions
    {
        public Strategy Strategy { get; }
        public bool KeepStandardLegal { get; }
        public bool PreferGoldenForPlayset { get; }

        public AdvisorOptions(Strategy strategy = Strategy.SafeOnly,
                              bool keepStandardLegal = true,
                              bool preferGoldenForPlayset = true)
        {
            Strategy = strategy;
            KeepStandardLegal = keepStandardLegal;
            PreferGoldenForPlayset = preferGoldenForPlayset;
        }
    }
}
