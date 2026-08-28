using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>فیلترهای مشترک لیست تأیید ادمین (قالب / پیام).</summary>
    public static class AdminApprovalListFilters
    {
        public static IQueryable<MessageTemplate> ApplyTemplateFilters(
            IQueryable<MessageTemplate> query,
            string? search,
            string? userSearch)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(t =>
                    t.Name.Contains(term)
                    || t.Content.Contains(term)
                    || (t.Description != null && t.Description.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(userSearch))
            {
                var userTerm = userSearch.Trim();
                if (int.TryParse(userTerm, out var userId) && userTerm.Length <= 9 && !userTerm.StartsWith('0'))
                {
                    query = query.Where(t => t.UserId == userId);
                }
                else
                {
                    query = query.Where(t =>
                        (t.User.FullName != null && t.User.FullName.Contains(userTerm))
                        || t.User.PhoneNumber.Contains(userTerm));
                }
            }

            return query;
        }

        public static IQueryable<SmsApprovalRequest> ApplyMessageApprovalFilters(
            IQueryable<SmsApprovalRequest> query,
            string? search,
            string? userSearch)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(r =>
                    r.ContentPreview.Contains(term)
                    || (r.TitlePreview != null && r.TitlePreview.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(userSearch))
            {
                var userTerm = userSearch.Trim();
                if (int.TryParse(userTerm, out var userId) && userTerm.Length <= 9 && !userTerm.StartsWith('0'))
                {
                    query = query.Where(r => r.UserId == userId);
                }
                else
                {
                    query = query.Where(r =>
                        (r.User.FullName != null && r.User.FullName.Contains(userTerm))
                        || r.User.PhoneNumber.Contains(userTerm));
                }
            }

            return query;
        }
    }
}
