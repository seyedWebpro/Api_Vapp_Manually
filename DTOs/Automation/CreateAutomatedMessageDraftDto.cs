using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای ایجاد Draft پیام خودکار (مرحله 1 - انتخاب نوع پیام)
    /// </summary>
    public class CreateAutomatedMessageDraftDto
    {
        [Required(ErrorMessage = "نوع اتوماسیون الزامی است")]
        public string AutomationType { get; set; } = string.Empty;
        // Birthday, CashbackExpiry, Welcome, PurchaseReminder, SpecialOccasion, Custom
    }
}

