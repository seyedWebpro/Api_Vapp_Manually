using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// انتخاب تصادفی وزن‌دار جایزه گردونه
    /// </summary>
    public static class LuckyWheelSpinHelper
    {
        public static LuckyWheelItem PickWeightedItem(IReadOnlyList<LuckyWheelItem> items)
        {
            if (items.Count == 0)
            {
                throw new InvalidOperationException("گردونه بدون جایزه قابل چرخش نیست");
            }

            var ordered = items.OrderBy(i => i.DisplayOrder).ToList();
            var roll = (decimal)(Random.Shared.NextDouble() * 100.0);
            decimal cumulative = 0m;

            foreach (var item in ordered)
            {
                cumulative += item.Probability;
                if (roll < cumulative)
                {
                    return item;
                }
            }

            return ordered[^1];
        }

        /// <summary>
        /// تولید کاندید کد جایزه خوانا (مثلاً LW-A3K9P2)
        /// </summary>
        public static string CreatePrizeCodeCandidate()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Span<char> chars = stackalloc char[6];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
            }

            return $"LW-{new string(chars)}";
        }
    }
}
