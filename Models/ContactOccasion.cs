using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Vapp.Models
{
    /// <summary>
    /// مناسبت‌های مربوط به مخاطب
    /// مانند تاریخ تولد، ازدواج، وفات و غیره
    /// </summary>
    public class ContactOccasion
    {
        public int Id { get; set; }

        public int ContactId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty; // عنوان مناسبت (تولد، ازدواج، ...)

        public DateTime Date { get; set; } // تاریخ و زمان

        public bool HasTime { get; set; } = false; // آیا زمان مشخص شده مهم است؟

        #region Navigation Properties

        [ForeignKey("ContactId")]
        public virtual Contact Contact { get; set; } = null!;

        #endregion
    }
}


























