namespace Api_Vapp.DTOs.UserRole
{
    /// <summary>
    /// DTO برای به‌روزرسانی رابطه کاربر-نقش
    /// تمام فیلدها اختیاری هستند - اگر null یا empty باشند، مقدار قبلی تغییر نمی‌کند
    /// </summary>
    public class UpdateUserRoleDto
    {
        public bool? IsActive { get; set; }
    }
}

