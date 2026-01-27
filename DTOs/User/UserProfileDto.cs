using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.User
{
    /// <summary>
    /// DTO برای اطلاعات کامل پروفایل کاربر
    /// </summary>
    public class UserProfileDto
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? NationalId { get; set; }
        public string? Email { get; set; }
        public string? ProfileImagePath { get; set; }
        public string? ProfileImageUrl { get; set; }
        public decimal WalletBalance { get; set; }
        public string FormattedWalletBalance { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsPhoneVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    /// <summary>
    /// DTO برای به‌روزرسانی پروفایل کاربر
    /// </summary>
    public class UpdateUserProfileDto
    {
        public string? FullName { get; set; }

        [RegularExpression(@"^\d{10}$", ErrorMessage = "فرمت کد ملی صحیح نیست")]
        public string? NationalId { get; set; }

        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تلفن صحیح نیست")]
        public string? PhoneNumber { get; set; }
    }
}



