using System.Globalization;
using System.Text;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Ui.Export
{
    public static class CsvExporter
    {
        public static string ToCsv(DustPlan plan)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CardId,Name,Rarity,RegularToDust,GoldenToDust,DustGained,InRefundWindow,IsStandardLegal");
            foreach (var i in plan.Items)
            {
                sb.AppendLine(string.Join(",",
                    Escape(i.CardId), Escape(i.CardName), i.Rarity,
                    i.RegularToDust.ToString(CultureInfo.InvariantCulture),
                    i.GoldenToDust.ToString(CultureInfo.InvariantCulture),
                    i.DustGained.ToString(CultureInfo.InvariantCulture),
                    i.InRefundWindow, i.IsStandardLegal));
            }
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (s == null) return string.Empty;
            if (s.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
