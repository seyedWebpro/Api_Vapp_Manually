namespace Api_Vapp.Constants
{
    /// <summary>
    /// دلایل عدم ارسال پیامک یادآوری نوبت — برای منطق UI بدون وابستگی به متن فارسی.
    /// </summary>
    public static class BookingReminderSkipReasons
    {
        public const string Disabled = "DISABLED";
        public const string Cancelled = "CANCELLED";
        public const string Past = "PAST";
        public const string NotConfirmed = "NOT_CONFIRMED";
        public const string AlreadySent = "ALREADY_SENT";
    }
}
