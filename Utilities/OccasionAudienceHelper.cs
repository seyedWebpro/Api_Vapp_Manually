using System.Text.Json;
using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// ذخیره/خواندن و فیلتر محدوده مخاطبین هر مناسبت
    /// </summary>
    public static class OccasionAudienceHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static List<int> ParseIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                var ids = JsonSerializer.Deserialize<List<int>>(json, JsonOptions);
                return ids?
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ?? [];
            }
            catch
            {
                return [];
            }
        }

        public static string? SerializeIds(IEnumerable<int>? ids)
        {
            var list = ids?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? [];

            return list.Count == 0 ? null : JsonSerializer.Serialize(list);
        }

        public static bool IsContactInAudience(
            Contact contact,
            bool applyToAllContacts,
            IReadOnlyCollection<int> notebookIds,
            IReadOnlyCollection<int> contactIds,
            IReadOnlyCollection<int> excludedContactIds)
        {
            if (excludedContactIds.Contains(contact.Id))
                return false;

            if (applyToAllContacts)
                return true;

            if (contactIds.Count > 0)
                return contactIds.Contains(contact.Id);

            if (notebookIds.Count > 0)
                return notebookIds.Contains(contact.ContactNotebookId);

            // انتخاب ناقص: هیچ گیرنده‌ای
            return false;
        }

        public static bool IsContactInAudience(Contact contact, UserOccasionPreference? preference)
        {
            if (preference == null)
            {
                // بدون Preference → همه مخاطبین (رفتار قبلی اتوماسیون)
                return true;
            }

            return IsContactInAudience(
                contact,
                preference.ApplyToAllContacts,
                ParseIds(preference.ContactNotebookIdsJson),
                ParseIds(preference.ContactIdsJson),
                ParseIds(preference.ExcludedContactIdsJson));
        }
    }
}
