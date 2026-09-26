using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface INumberSeekerPhoneBankRepository
    {
        Task<int> UpsertPhonesAsync(
            IReadOnlyList<string> phones,
            string source,
            string city,
            string category,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// تخصیص شماره از بانک (شهر+دسته، و در صورت نیاز منبع). شماره‌ها در بانک می‌مانند؛ فقط ServedCount افزایش می‌یابد.
        /// </summary>
        Task<List<string>> AllocateAsync(
            string city,
            string category,
            string source,
            int maxPhones,
            CancellationToken cancellationToken = default);

        Task<(int Total, int Available)> CountAsync(
            string? city = null,
            string? category = null,
            string? source = null,
            CancellationToken cancellationToken = default);

        Task<List<NumberSeekerPhoneBankStatRow>> GetStatsAsync(
            CancellationToken cancellationToken = default);

        Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

        Task<int> SoftDeleteByPhonesAsync(
            IReadOnlyList<string> phones,
            CancellationToken cancellationToken = default);

        Task<NumberSeekerPhoneBank?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    }

    public sealed class NumberSeekerPhoneBankStatRow
    {
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Available { get; set; }
    }
}
