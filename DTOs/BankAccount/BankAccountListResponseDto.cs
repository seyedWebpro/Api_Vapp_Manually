namespace Api_Vapp.DTOs.BankAccount
{
    /// <summary>
    /// لیست صفحه‌بندی‌شده شماره حساب‌ها
    /// </summary>
    public class BankAccountListResponseDto
    {
        public List<BankAccountResponseDto> BankAccounts { get; set; } = new();

        public int TotalCount { get; set; }

        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int TotalPages { get; set; }
    }
}
