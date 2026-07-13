using Api_Vapp.Data;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    /// <summary>
    /// ذخیره شرکت‌کننده عمومی در دفترچه تلفن مالک
    /// </summary>
    public class PublicPhonebookService
    {
        private readonly Api_Context _context;

        public PublicPhonebookService(Api_Context context)
        {
            _context = context;
        }

        public async Task<int?> SaveParticipantAsync(
            IReadOnlyList<int> notebookIds,
            string mobile,
            string fullName)
        {
            if (notebookIds.Count == 0)
            {
                return null;
            }

            var existingContacts = await _context.Contacts
                .Where(c =>
                    notebookIds.Contains(c.ContactNotebookId) &&
                    c.MobileNumber == mobile &&
                    !c.IsDeleted)
                .ToListAsync();

            var existingByNotebook = existingContacts.ToDictionary(c => c.ContactNotebookId);
            Contact? savedContact = existingContacts.FirstOrDefault();
            var now = DateTime.UtcNow;

            foreach (var notebookId in notebookIds)
            {
                if (existingByNotebook.TryGetValue(notebookId, out var existing))
                {
                    savedContact ??= existing;
                    if (string.IsNullOrWhiteSpace(existing.FullName) && !string.IsNullOrWhiteSpace(fullName))
                    {
                        existing.FullName = fullName;
                        existing.UpdatedAt = now;
                    }
                }
                else
                {
                    var contact = new Contact
                    {
                        ContactNotebookId = notebookId,
                        MobileNumber = mobile,
                        FullName = fullName,
                        CreatedAt = now
                    };

                    await _context.Contacts.AddAsync(contact);
                    savedContact ??= contact;
                }
            }

            await _context.SaveChangesAsync();
            return savedContact?.Id;
        }
    }
}
