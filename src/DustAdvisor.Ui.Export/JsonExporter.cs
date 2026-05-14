using DustAdvisor.Algorithm.Domain;
using Newtonsoft.Json;

namespace DustAdvisor.Ui.Export
{
    public static class JsonExporter
    {
        public static string ToJson(DustPlan plan)
            => JsonConvert.SerializeObject(plan, Formatting.Indented);
    }
}
