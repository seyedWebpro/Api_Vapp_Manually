using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BankAccount
{
    /// <summary>
    /// DTO ارسال سریع شماره حساب به یک مخاطب
    /// </summary>
    public class QuickSendBankAccountDto
    {
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }

        [Required(ErrorMessage = "شناسه شماره حساب الزامی است")]
        public int BankAccountId { get; set; }
    }
}
