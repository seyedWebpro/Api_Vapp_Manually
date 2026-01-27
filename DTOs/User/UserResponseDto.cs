namespace Api_Vapp.DTOs.User
{
    /// <summary>
    /// DTO برای پاسخ اطلاعات کاربر
    /// </summary>
    public class UserResponseDto
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? NationalId { get; set; }
        public string? Email { get; set; }
        public string? ProfileImagePath { get; set; }
        public string? ProfileImageUrl { get; set; }
        public bool IsActive { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsDeleted { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}

