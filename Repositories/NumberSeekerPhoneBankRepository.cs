using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class NumberSeekerPhoneBankRepository : INumberSeekerPhoneBankRepository
    {
        private readonly Api_Context _context;

        public NumberSeekerPhoneBankRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<int> UpsertPhonesAsync(
            IReadOnlyList<string> phones,
            string source,
            string city,
            string category,
            CancellationToken cancellationToken = default)
        {
            if (phones == null || phones.Count == 0)
                return 0;

            var cleaned = phones
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizePhone)
                .Where(p => p.Length >= 8)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (cleaned.Count == 0)
                return 0;

            var sourceNorm = (source ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(sourceNorm))
                sourceNorm = "manual";

            var cityNorm = (city ?? string.Empty).Trim();
            var categoryNorm = (category ?? string.Empty).Trim();

            try
            {
                return await UpsertCoreAsync(cleaned, sourceNorm, cityNorm, categoryNorm, cancellationToken);
            }
            catch (DbUpdateException)
            {
                // race روی UNIQUE PhoneNumber
                _context.ChangeTracker.Clear();
                return await UpsertCoreAsync(cleaned, sourceNorm, cityNorm, categoryNorm, cancellationToken);
            }
        }

        private async Task<int> UpsertCoreAsync(
            List<string> cleaned,
            string sourceNorm,
            string cityNorm,
            string categoryNorm,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var inserted = 0;

            var existing = await _context.NumberSeekerPhoneBanks
                .Where(p => cleaned.Contains(p.PhoneNumber))
                .ToListAsync(cancellationToken);

            var byNumber = existing.ToDictionary(p => p.PhoneNumber, StringComparer.Ordinal);

            foreach (var phone in cleaned)
            {
                if (byNumber.TryGetValue(phone, out var row))
                {
                    row.UpdatedAt = now;
                    if (row.IsDeleted)
                    {
                        row.IsDeleted = false;
                        row.IsAvailable = true;
                    }

                    if (!string.IsNullOrWhiteSpace(cityNorm))
                        row.City = cityNorm;

                    if (!string.IsNullOrWhiteSpace(categoryNorm))
                        row.Category = categoryNorm;

                    if (!string.IsNullOrWhiteSpace(sourceNorm) && sourceNorm != "unknown")
                        row.Source = sourceNorm;
                }
                else
                {
                    _context.NumberSeekerPhoneBanks.Add(new NumberSeekerPhoneBank
                    {
                        PhoneNumber = phone,
                        Source = sourceNorm,
                        City = cityNorm,
                        Category = categoryNorm,
                        IsAvailable = true,
                        ServedCount = 0,
                        CreatedAt = now,
                        IsDeleted = false
                    });
                    inserted++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return inserted;
        }

        public async Task<List<string>> AllocateAsync(
            string city,
            string category,
            string source,
            int maxPhones,
            CancellationToken cancellationToken = default)
        {
            if (maxPhones < 1)
                return new List<string>();

            var take = Math.Clamp(maxPhones, 1, 1000);
            var cityNorm = (city ?? string.Empty).Trim();
            var categoryNorm = (category ?? string.Empty).Trim();
            var sourceNorm = (source ?? string.Empty).Trim().ToLowerInvariant();

            var query = _context.NumberSeekerPhoneBanks
                .Where(p => !p.IsDeleted && p.IsAvailable
                            && p.City == cityNorm
                            && p.Category == categoryNorm);

            if (!string.IsNullOrEmpty(sourceNorm) && sourceNorm != "all")
                query = query.Where(p => p.Source == sourceNorm);

            var rows = await query
                .OrderBy(p => p.ServedCount)
                .ThenBy(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
                return new List<string>();

            var now = DateTime.UtcNow;
            foreach (var row in rows)
            {
                row.ServedCount += 1;
                row.LastServedAt = now;
                row.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return rows.Select(r => r.PhoneNumber).ToList();
        }

        public async Task<(int Total, int Available)> CountAsync(
            string? city = null,
            string? category = null,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.NumberSeekerPhoneBanks
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(city))
                query = query.Where(p => p.City == city.Trim());

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category.Trim());

            if (!string.IsNullOrWhiteSpace(source) &&
                !string.Equals(source.Trim(), "all", StringComparison.OrdinalIgnoreCase))
            {
                var s = source.Trim().ToLowerInvariant();
                query = query.Where(p => p.Source == s);
            }

            var total = await query.CountAsync(cancellationToken);
            var available = await query.CountAsync(p => p.IsAvailable, cancellationToken);
            return (total, available);
        }

        public async Task<List<NumberSeekerPhoneBankStatRow>> GetStatsAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.NumberSeekerPhoneBanks
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .GroupBy(p => new { p.City, p.Category, p.Source })
                .Select(g => new NumberSeekerPhoneBankStatRow
                {
                    City = g.Key.City,
                    Category = g.Key.Category,
                    Source = g.Key.Source,
                    Total = g.Count(),
                    Available = g.Count(x => x.IsAvailable)
                })
                .OrderBy(r => r.City)
                .ThenBy(r => r.Category)
                .ThenBy(r => r.Source)
                .ToListAsync(cancellationToken);
        }

        public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var row = await _context.NumberSeekerPhoneBanks
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (row == null)
                return;

            row.IsDeleted = true;
            row.IsAvailable = false;
            row.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> SoftDeleteByPhonesAsync(
            IReadOnlyList<string> phones,
            CancellationToken cancellationToken = default)
        {
            if (phones == null || phones.Count == 0)
                return 0;

            var cleaned = phones
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizePhone)
                .Where(p => p.Length >= 8)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (cleaned.Count == 0)
                return 0;

            var rows = await _context.NumberSeekerPhoneBanks
                .Where(p => !p.IsDeleted && cleaned.Contains(p.PhoneNumber))
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            foreach (var row in rows)
            {
                row.IsDeleted = true;
                row.IsAvailable = false;
                row.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return rows.Count;
        }

        public Task<NumberSeekerPhoneBank?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return _context.NumberSeekerPhoneBanks
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        }

        private static string NormalizePhone(string raw)
        {
            var mapped = raw.Trim().Select(c => c switch
            {
                '۰' => '0', '۱' => '1', '۲' => '2', '۳' => '3', '۴' => '4',
                '۵' => '5', '۶' => '6', '۷' => '7', '۸' => '8', '۹' => '9',
                '٠' => '0', '١' => '1', '٢' => '2', '٣' => '3', '٤' => '4',
                '٥' => '5', '٦' => '6', '٧' => '7', '٨' => '8', '٩' => '9',
                _ => c
            }).Where(c => char.IsDigit(c) || c == '+').ToArray();
            return new string(mapped);
        }
    }
}
