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
    }
}
