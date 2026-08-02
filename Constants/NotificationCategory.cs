namespace Api_Vapp.Constants
{
    /// <summary>
    /// نوع اعلان push — متناظر با فلگ‌های UserNotificationSettings
    /// </summary>
    public enum NotificationCategory
    {
        ImportantNotifications = 1,
        Updates = 2,
        SystemWarnings = 3,
        WalletTransaction = 4,
        CustomerCashback = 5,
        FinancialReport = 6,
        NewCustomerRegistration = 7,
        Suggestions = 8,
        EducationAndTips = 9
    }
}
