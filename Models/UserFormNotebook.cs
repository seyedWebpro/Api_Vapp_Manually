namespace Api_Vapp.Models
{
    /// <summary>
    /// ارتباط فرم با دفترچه‌های تلفن برای ذخیره تکمیل‌کنندگان
    /// </summary>
    public class UserFormNotebook
    {
        public int UserFormId { get; set; }

        public int ContactNotebookId { get; set; }

        public virtual UserForm UserForm { get; set; } = null!;

        public virtual ContactNotebook ContactNotebook { get; set; } = null!;
    }
}
