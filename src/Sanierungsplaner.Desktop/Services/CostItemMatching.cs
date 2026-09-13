using System.Text;
using System.Text.RegularExpressions;
using Sanierungsplaner.Desktop.Models;

namespace Sanierungsplaner.Desktop.Services;

public static class CostItemMatching
{
    public static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ").ToUpperInvariant();
    public static (string Material, string Unit, string Floor, string Room) Key(CostItem item)
        => (Normalize(item.Material), Normalize(item.Unit), Normalize(item.Floor), Normalize(item.Room));
    public static IReadOnlyList<CostItem> Similar(CostItem incoming, IEnumerable<CostItem> existing)
    {
        var key = Key(incoming);
        return existing.GroupBy(Key).Select(g => g.First()).Where(candidate =>
        {
            var other = Key(candidate);
            if (other.Unit != key.Unit || other.Floor != key.Floor || other.Room != key.Room) return false;
            if (Regex.Replace(key.Material, @"\D", "") != Regex.Replace(other.Material, @"\D", "")) return false;
            var limit = Math.Min(key.Material.Length, other.Material.Length) >= 8 ? 2 : 1;
            return Math.Min(key.Material.Length, other.Material.Length) >= 4
                && Math.Abs(key.Material.Length - other.Material.Length) <= limit
                && Distance(key.Material, other.Material) <= limit;
        }).OrderBy(c => Distance(key.Material, Normalize(c.Material))).ThenBy(c => c.Material).ToArray();
    }
    // Optimal-string-alignment distance also recognizes adjacent transpositions such as Estrcih.
    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
            {
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }
}
