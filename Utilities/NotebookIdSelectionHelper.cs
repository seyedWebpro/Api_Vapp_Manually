using System.Text.Json;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نرمال‌سازی و خواندن شناسه دفترچه‌های انتخاب‌شده (تکی یا چندتایی).
    /// </summary>
    public static class NotebookIdSelectionHelper
    {
        public static List<int> Normalize(IEnumerable<int>? notebookIds, int? singleNotebookId = null)
        {
            var ids = new List<int>();
            if (notebookIds != null)
            {
                ids.AddRange(notebookIds);
            }

            if (singleNotebookId.HasValue)
            {
                ids.Add(singleNotebookId.Value);
            }

            return ids.Where(id => id > 0).Distinct().ToList();
        }

        public static HashSet<int> ReadFromJsonElement(JsonElement root)
        {
            var ids = new HashSet<int>();

            if (TryGetProperty(root, "ContactNotebookIds", "contactNotebookIds", out var arrayProp)
                && arrayProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arrayProp.EnumerateArray())
                {
                    if (item.TryGetInt32(out var id) && id > 0)
                    {
                        ids.Add(id);
                    }
                }
            }

            if (TryGetProperty(root, "ContactNotebookId", "contactNotebookId", out var singleProp)
                && singleProp.ValueKind != JsonValueKind.Null
                && singleProp.TryGetInt32(out var singleId)
                && singleId > 0)
            {
                ids.Add(singleId);
            }

            return ids;
        }

        private static bool TryGetProperty(
            JsonElement root,
            string pascalName,
            string camelName,
            out JsonElement value)
        {
            if (root.TryGetProperty(pascalName, out value))
            {
                return true;
            }

            return root.TryGetProperty(camelName, out value);
        }
    }
}
