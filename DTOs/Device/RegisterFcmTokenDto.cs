using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Device
{
    /// <summary>
    /// ثبت یا به‌روزرسانی توکن FCM دستگاه
    /// </summary>
    public class RegisterFcmTokenDto
    {
        /// <summary>
        /// توکن FCM (استرینگ یکتا از Firebase Messaging)
        /// </summary>
        [Required(ErrorMessage = "توکن الزامی است")]
        [MinLength(1, ErrorMessage = "توکن نمی‌تواند خالی باشد")]
        [MaxLength(512, ErrorMessage = "توکن بیش از حد طولانی است")]
        public string Token { get; set; } = string.Empty;
    }
}
