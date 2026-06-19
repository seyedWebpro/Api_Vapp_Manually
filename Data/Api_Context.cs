using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Data
{
    public class Api_Context : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<ContactNotebook> ContactNotebooks { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ContactAdditionalInfo> ContactAdditionalInfos { get; set; }
        public DbSet<ContactOccasion> ContactOccasions { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<MessageTemplate> MessageTemplates { get; set; }
        public DbSet<TemplateGroup> TemplateGroups { get; set; }
        public DbSet<MessageCampaign> MessageCampaigns { get; set; }
        public DbSet<MessageRecipient> MessageRecipients { get; set; }
        public DbSet<MessageSession> MessageSessions { get; set; }
        public DbSet<AutomatedMessage> AutomatedMessages { get; set; }
        public DbSet<AutomationExecution> AutomationExecutions { get; set; }
        public DbSet<SpecialOccasion> SpecialOccasions { get; set; }
        public DbSet<QuickAction> QuickActions { get; set; }
        public DbSet<SocialMediaLink> SocialMediaLinks { get; set; }
        public DbSet<MessageTag> MessageTags { get; set; }
        public DbSet<ContactTag> ContactTags { get; set; }

        // کیف پول و مالی
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Cashback> Cashbacks { get; set; }
        public DbSet<CashbackTransaction> CashbackTransactions { get; set; }
        public DbSet<CashbackDraft> CashbackDrafts { get; set; }
        public DbSet<UserNotificationSettings> UserNotificationSettings { get; set; }
        public DbSet<ContactCashbackBalance> ContactCashbackBalances { get; set; }
        public DbSet<ManualCashbackTransaction> ManualCashbackTransactions { get; set; }

        // پنل ادمین
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<TicketMessage> TicketMessages { get; set; }
        public DbSet<EducationalVideo> EducationalVideos { get; set; }
        public DbSet<SmsApprovalRequest> SmsApprovalRequests { get; set; }

        public Api_Context(DbContextOptions<Api_Context> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // تنظیمات User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Id).ValueGeneratedOnAdd();

                entity.Property(u => u.PhoneNumber)
                    .IsRequired();

                entity.Property(u => u.PasswordHash)
                    .IsRequired();

                entity.Property(u => u.IsActive)
                    .HasDefaultValue(true);

                entity.Property(u => u.IsPhoneVerified)
                    .HasDefaultValue(false);

                entity.Property(u => u.IsDeleted)
                    .HasDefaultValue(false);

                entity.Property(u => u.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");
                
                // تنظیم Precision برای WalletBalance
                entity.Property(u => u.WalletBalance)
                    .HasPrecision(18, 2)
                    .HasDefaultValue(0);

                entity.Property(u => u.ProfileImagePath)
                    .HasMaxLength(500);

                // ایندکس‌ها
                entity.HasIndex(u => u.PhoneNumber).IsUnique();
                entity.HasIndex(u => u.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
                entity.HasIndex(u => u.IsActive);
                entity.HasIndex(u => u.IsDeleted);
            });

            // تنظیمات RefreshToken
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(rt => rt.Id);
                entity.Property(rt => rt.Id).ValueGeneratedOnAdd();

                entity.Property(rt => rt.Token)
                    .IsRequired();

                entity.Property(rt => rt.UserId)
                    .IsRequired();

                entity.Property(rt => rt.ExpiresAt)
                    .IsRequired();

                entity.Property(rt => rt.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(rt => rt.IsRevoked)
                    .HasDefaultValue(false);

                // رابطه با User
                entity.HasOne(rt => rt.User)
                    .WithMany()
                    .HasForeignKey(rt => rt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ایندکس‌ها
                entity.HasIndex(rt => rt.Token).IsUnique();
                entity.HasIndex(rt => rt.UserId);
                entity.HasIndex(rt => rt.IsRevoked);
            });

            // تنظیمات Role
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).ValueGeneratedOnAdd();

                entity.Property(r => r.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(r => r.DisplayName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(r => r.Description)
                    .HasMaxLength(500);

                entity.Property(r => r.IsActive)
                    .HasDefaultValue(true);

                entity.Property(r => r.IsDeleted)
                    .HasDefaultValue(false);

                entity.Property(r => r.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // ایندکس‌ها
                entity.HasIndex(r => r.Name).IsUnique();
                entity.HasIndex(r => r.IsActive);
                entity.HasIndex(r => r.IsDeleted);
            });

            // تنظیمات UserRole
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasKey(ur => ur.Id);
                entity.Property(ur => ur.Id).ValueGeneratedOnAdd();

                entity.Property(ur => ur.UserId)
                    .IsRequired();

                entity.Property(ur => ur.RoleId)
                    .IsRequired();

                entity.Property(ur => ur.IsActive)
                    .HasDefaultValue(true);

                entity.Property(ur => ur.IsDeleted)
                    .HasDefaultValue(false);

                entity.Property(ur => ur.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // رابطه با User
                entity.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // رابطه با Role
                entity.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ایندکس‌ها
                entity.HasIndex(ur => ur.UserId);
                entity.HasIndex(ur => ur.RoleId);
                entity.HasIndex(ur => ur.IsActive);
                entity.HasIndex(ur => ur.IsDeleted);

                // ایندکس ترکیبی برای جلوگیری از تکراری بودن نقش برای یک کاربر
                entity.HasIndex(ur => new { ur.UserId, ur.RoleId })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");
            });

            // تنظیمات ContactNotebook
            modelBuilder.Entity<ContactNotebook>(entity =>
            {
                entity.HasKey(cn => cn.Id);
                entity.Property(cn => cn.Id).ValueGeneratedOnAdd();

                entity.Property(cn => cn.UserId)
                    .IsRequired();

                entity.Property(cn => cn.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(cn => cn.Description)
                    .HasMaxLength(1000);

                entity.Property(cn => cn.Icon)
                    .HasMaxLength(500);

                entity.Property(cn => cn.IsActive)
                    .HasDefaultValue(true);

                entity.Property(cn => cn.IsDeleted)
                    .HasDefaultValue(false);

                entity.Property(cn => cn.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // رابطه با User
                entity.HasOne(cn => cn.User)
                    .WithMany()
                    .HasForeignKey(cn => cn.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ایندکس‌ها
                entity.HasIndex(cn => cn.UserId);
                entity.HasIndex(cn => cn.IsActive);
                entity.HasIndex(cn => cn.IsDeleted);
                entity.HasIndex(cn => new { cn.UserId, cn.Name })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");
            });

            // تنظیمات Contact
            modelBuilder.Entity<Contact>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).ValueGeneratedOnAdd();

                entity.Property(c => c.ContactNotebookId)
                    .IsRequired();

                entity.Property(c => c.MobileNumber)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(c => c.FullName)
                    .HasMaxLength(200);

                entity.Property(c => c.Brand)
                    .HasMaxLength(200);

                entity.Property(c => c.ProfileImagePath)
                    .HasMaxLength(500);

                entity.Property(c => c.IsDeleted)
                    .HasDefaultValue(false);

                entity.Property(c => c.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // رابطه با ContactNotebook
                entity.HasOne(c => c.ContactNotebook)
                    .WithMany(cn => cn.Contacts)
                    .HasForeignKey(c => c.ContactNotebookId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ایندکس‌ها
                entity.HasIndex(c => c.ContactNotebookId);
                entity.HasIndex(c => c.MobileNumber);
                entity.HasIndex(c => c.IsDeleted);
                entity.HasIndex(c => new { c.ContactNotebookId, c.MobileNumber })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");
            });

            // تنظیمات ContactAdditionalInfo
            modelBuilder.Entity<ContactAdditionalInfo>(entity =>
            {
                entity.HasKey(cai => cai.Id);
                entity.Property(cai => cai.Id).ValueGeneratedOnAdd();

                entity.Property(cai => cai.ContactId)
                    .IsRequired();

                entity.Property(cai => cai.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // رابطه با Contact
                entity.HasOne(cai => cai.Contact)
                    .WithOne(c => c.AdditionalInfo)
                    .HasForeignKey<ContactAdditionalInfo>(cai => cai.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ایندکس‌ها
                entity.HasIndex(cai => cai.ContactId).IsUnique();
            });

            // تنظیمات ContactOccasion
            modelBuilder.Entity<ContactOccasion>(entity =>
            {
                entity.HasKey(co => co.Id);
                entity.Property(co => co.Id).ValueGeneratedOnAdd();

                entity.Property(co => co.ContactId).IsRequired();
                entity.Property(co => co.Title).IsRequired().HasMaxLength(100);
                entity.Property(co => co.Date).IsRequired();
                entity.Property(co => co.HasTime).HasDefaultValue(false);

                // رابطه با Contact
                entity.HasOne(co => co.Contact)
                    .WithMany(c => c.Occasions)
                    .HasForeignKey(co => co.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ایندکس‌ها
                entity.HasIndex(co => co.ContactId);
            });

            // تنظیمات Message
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Id).ValueGeneratedOnAdd();

                entity.Property(m => m.UserId).IsRequired();
                entity.Property(m => m.Content).IsRequired().HasMaxLength(2000);
                entity.Property(m => m.Status).HasMaxLength(50).HasDefaultValue("Draft");
                entity.Property(m => m.IsDeleted).HasDefaultValue(false);
                entity.Property(m => m.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(m => m.User)
                    .WithMany()
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Template)
                    .WithMany(t => t.Messages)
                    .HasForeignKey(m => m.TemplateId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(m => m.UserId);
                entity.HasIndex(m => m.Status);
                entity.HasIndex(m => m.IsDeleted);
            });

            // تنظیمات TemplateGroup
            modelBuilder.Entity<TemplateGroup>(entity =>
            {
                entity.HasKey(tg => tg.Id);
                entity.Property(tg => tg.Id).ValueGeneratedOnAdd();

                entity.Property(tg => tg.UserId).IsRequired();
                entity.Property(tg => tg.Name).IsRequired().HasMaxLength(200);
                entity.Property(tg => tg.Description).HasMaxLength(500);
                entity.Property(tg => tg.DisplayOrder).HasDefaultValue(0);
                entity.Property(tg => tg.IsActive).HasDefaultValue(true);
                entity.Property(tg => tg.IsDeleted).HasDefaultValue(false);
                entity.Property(tg => tg.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(tg => tg.User)
                    .WithMany()
                    .HasForeignKey(tg => tg.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(tg => tg.UserId);
                entity.HasIndex(tg => tg.IsDeleted);
                entity.HasIndex(tg => tg.DisplayOrder);
            });

            // تنظیمات MessageTemplate
            modelBuilder.Entity<MessageTemplate>(entity =>
            {
                entity.HasKey(mt => mt.Id);
                entity.Property(mt => mt.Id).ValueGeneratedOnAdd();

                entity.Property(mt => mt.UserId).IsRequired();
                entity.Property(mt => mt.Name).IsRequired().HasMaxLength(200);
                entity.Property(mt => mt.Content).IsRequired().HasMaxLength(2000);
                entity.Property(mt => mt.Category).HasMaxLength(100);
                entity.Property(mt => mt.IsActive).HasDefaultValue(true);
                entity.Property(mt => mt.IsDeleted).HasDefaultValue(false);
                entity.Property(mt => mt.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(mt => mt.User)
                    .WithMany()
                    .HasForeignKey(mt => mt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(mt => mt.Group)
                    .WithMany(tg => tg.Templates)
                    .HasForeignKey(mt => mt.GroupId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(mt => mt.UserId);
                entity.HasIndex(mt => mt.GroupId);
                entity.HasIndex(mt => mt.IsDeleted);
            });

            // تنظیمات MessageCampaign
            modelBuilder.Entity<MessageCampaign>(entity =>
            {
                entity.HasKey(mc => mc.Id);
                entity.Property(mc => mc.Id).ValueGeneratedOnAdd();

                entity.Property(mc => mc.MessageId).IsRequired();
                entity.Property(mc => mc.UserId).IsRequired();
                entity.Property(mc => mc.SendType).HasMaxLength(50).HasDefaultValue("Quick");
                entity.Property(mc => mc.Status).HasMaxLength(50).HasDefaultValue("Draft");
                entity.Property(mc => mc.WalletStatus).HasMaxLength(50);
                entity.Property(mc => mc.PreventDuplicate).HasDefaultValue(false);
                entity.Property(mc => mc.DuplicatePreventionHours).HasDefaultValue(24);
                entity.Property(mc => mc.SendToSpecificTags).HasDefaultValue(false);
                entity.Property(mc => mc.IsDeleted).HasDefaultValue(false);
                entity.Property(mc => mc.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                // تنظیم Precision برای فیلدهای decimal
                entity.Property(mc => mc.CostPerPart).HasPrecision(18, 2);
                entity.Property(mc => mc.EstimatedTotalCost).HasPrecision(18, 2);
                entity.Property(mc => mc.ActualTotalCost).HasPrecision(18, 2);

                entity.HasOne(mc => mc.Message)
                    .WithMany(m => m.Campaigns)
                    .HasForeignKey(mc => mc.MessageId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(mc => mc.User)
                    .WithMany()
                    .HasForeignKey(mc => mc.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(mc => mc.AutomatedMessage)
                    .WithMany(am => am.Campaigns)
                    .HasForeignKey(mc => mc.AutomatedMessageId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(mc => mc.MessageId);
                entity.HasIndex(mc => mc.AutomatedMessageId);
                entity.HasIndex(mc => mc.UserId);
                entity.HasIndex(mc => mc.Status);
                entity.HasIndex(mc => mc.ScheduledAt);
                entity.HasIndex(mc => mc.IsDeleted);
            });

            // تنظیمات MessageRecipient
            modelBuilder.Entity<MessageRecipient>(entity =>
            {
                entity.HasKey(mr => mr.Id);
                entity.Property(mr => mr.Id).ValueGeneratedOnAdd();

                entity.Property(mr => mr.CampaignId).IsRequired();
                entity.Property(mr => mr.MobileNumber).IsRequired().HasMaxLength(20);
                entity.Property(mr => mr.Status).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(mr => mr.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(mr => mr.Campaign)
                    .WithMany(c => c.Recipients)
                    .HasForeignKey(mr => mr.CampaignId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(mr => mr.Contact)
                    .WithMany(c => c.MessageRecipients)
                    .HasForeignKey(mr => mr.ContactId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(mr => mr.CampaignId);
                entity.HasIndex(mr => mr.MobileNumber);
                entity.HasIndex(mr => mr.Status);
            });

            // تنظیمات MessageSession
            modelBuilder.Entity<MessageSession>(entity =>
            {
                entity.HasKey(ms => ms.Id);
                entity.Property(ms => ms.Id).ValueGeneratedOnAdd();

                entity.Property(ms => ms.MessageId).IsRequired();
                entity.Property(ms => ms.UserId).IsRequired();
                entity.Property(ms => ms.SelectionCriteria).IsRequired().HasColumnType("nvarchar(max)");
                entity.Property(ms => ms.RecipientsJson).HasColumnType("nvarchar(max)");
                entity.Property(ms => ms.IsUsed).HasDefaultValue(false);
                entity.Property(ms => ms.IsDeleted).HasDefaultValue(false);
                entity.Property(ms => ms.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(ms => ms.Message)
                    .WithMany()
                    .HasForeignKey(ms => ms.MessageId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ms => ms.User)
                    .WithMany()
                    .HasForeignKey(ms => ms.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(ms => ms.MessageId);
                entity.HasIndex(ms => ms.UserId);
                entity.HasIndex(ms => ms.IsDeleted);
                entity.HasIndex(ms => ms.IsUsed);
                entity.HasIndex(ms => ms.ExpiresAt);
                
                // تنظیم RowVersion برای Optimistic Concurrency
                entity.Property(ms => ms.RowVersion).IsRowVersion();
            });

            // تنظیمات AutomatedMessage
            modelBuilder.Entity<AutomatedMessage>(entity =>
            {
                entity.HasKey(am => am.Id);
                entity.Property(am => am.Id).ValueGeneratedOnAdd();

                entity.Property(am => am.UserId).IsRequired();
                entity.Property(am => am.AutomationType).IsRequired().HasMaxLength(50);
                entity.Property(am => am.Title).HasMaxLength(200);
                entity.Property(am => am.Status).HasMaxLength(50).HasDefaultValue("Draft");
                entity.Property(am => am.IsActive).HasDefaultValue(true);
                entity.Property(am => am.IsDeleted).HasDefaultValue(false);
                entity.Property(am => am.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(am => am.User)
                    .WithMany()
                    .HasForeignKey(am => am.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(am => am.Message)
                    .WithMany()
                    .HasForeignKey(am => am.MessageId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(am => am.SpecialOccasion)
                    .WithMany(so => so.AutomatedMessages)
                    .HasForeignKey(am => am.SpecialOccasionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(am => am.UserId);
                entity.HasIndex(am => am.AutomationType);
                entity.HasIndex(am => am.Status);
                entity.HasIndex(am => am.IsActive);
                entity.HasIndex(am => am.IsDeleted);
            });

            // تنظیمات AutomationExecution
            modelBuilder.Entity<AutomationExecution>(entity =>
            {
                entity.HasKey(ae => ae.Id);
                entity.Property(ae => ae.Id).ValueGeneratedOnAdd();

                entity.Property(ae => ae.AutomatedMessageId).IsRequired();
                entity.Property(ae => ae.Status).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(ae => ae.MessageContent).HasMaxLength(2000);
                entity.Property(ae => ae.ExecutedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(ae => ae.AutomatedMessage)
                    .WithMany(am => am.Executions)
                    .HasForeignKey(ae => ae.AutomatedMessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ae => ae.Contact)
                    .WithMany()
                    .HasForeignKey(ae => ae.ContactId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(ae => ae.AutomatedMessageId);
                entity.HasIndex(ae => ae.ContactId);
                entity.HasIndex(ae => ae.ExecutedAt);
            });

            // تنظیمات SpecialOccasion
            modelBuilder.Entity<SpecialOccasion>(entity =>
            {
                entity.HasKey(so => so.Id);
                entity.Property(so => so.Id).ValueGeneratedOnAdd();

                entity.Property(so => so.Name).IsRequired().HasMaxLength(200);
                entity.Property(so => so.Type).HasMaxLength(50).HasDefaultValue("Custom");
                entity.Property(so => so.OccasionDate).IsRequired();
                entity.Property(so => so.IsSystem).HasDefaultValue(false);
                entity.Property(so => so.IsActive).HasDefaultValue(true);
                entity.Property(so => so.IsDeleted).HasDefaultValue(false);
                entity.Property(so => so.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(so => so.User)
                    .WithMany()
                    .HasForeignKey(so => so.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(so => so.OccasionDate);
                entity.HasIndex(so => so.IsSystem);
                entity.HasIndex(so => so.IsDeleted);
            });

            // تنظیمات QuickAction
            modelBuilder.Entity<QuickAction>(entity =>
            {
                entity.HasKey(qa => qa.Id);
                entity.Property(qa => qa.Id).ValueGeneratedOnAdd();

                entity.Property(qa => qa.UserId).IsRequired();
                entity.Property(qa => qa.Name).IsRequired().HasMaxLength(200);
                entity.Property(qa => qa.ActionType).HasMaxLength(50);
                entity.Property(qa => qa.IsActive).HasDefaultValue(true);
                entity.Property(qa => qa.IsDefault).HasDefaultValue(false);
                entity.Property(qa => qa.IsDeleted).HasDefaultValue(false);
                entity.Property(qa => qa.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(qa => qa.User)
                    .WithMany()
                    .HasForeignKey(qa => qa.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(qa => qa.UserId);
                entity.HasIndex(qa => qa.ActionType);
                entity.HasIndex(qa => qa.IsActive);
                entity.HasIndex(qa => qa.IsDeleted);
            });

            // تنظیمات SocialMediaLink
            modelBuilder.Entity<SocialMediaLink>(entity =>
            {
                entity.HasKey(sml => sml.Id);
                entity.Property(sml => sml.Id).ValueGeneratedOnAdd();

                entity.Property(sml => sml.UserId).IsRequired();
                entity.Property(sml => sml.Platform).IsRequired().HasMaxLength(50);
                entity.Property(sml => sml.LinkUrl).IsRequired().HasMaxLength(500);
                entity.Property(sml => sml.IsActive).HasDefaultValue(true);
                entity.Property(sml => sml.IsDefault).HasDefaultValue(false);
                entity.Property(sml => sml.IsDeleted).HasDefaultValue(false);
                entity.Property(sml => sml.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(sml => sml.User)
                    .WithMany()
                    .HasForeignKey(sml => sml.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(sml => sml.UserId);
                entity.HasIndex(sml => sml.Platform);
                entity.HasIndex(sml => sml.IsActive);
                entity.HasIndex(sml => sml.IsDeleted);
                entity.HasIndex(sml => sml.IsDefault);
            });

            // تنظیمات MessageTag
            modelBuilder.Entity<MessageTag>(entity =>
            {
                entity.HasKey(mt => mt.Id);
                entity.Property(mt => mt.Id).ValueGeneratedOnAdd();

                entity.Property(mt => mt.UserId).IsRequired();
                entity.Property(mt => mt.Name).IsRequired().HasMaxLength(100);
                entity.Property(mt => mt.Color).HasMaxLength(7); // Hex color
                entity.Property(mt => mt.IsActive).HasDefaultValue(true);
                entity.Property(mt => mt.IsDeleted).HasDefaultValue(false);
                entity.Property(mt => mt.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(mt => mt.User)
                    .WithMany()
                    .HasForeignKey(mt => mt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(mt => mt.UserId);
                entity.HasIndex(mt => mt.IsDeleted);
                entity.HasIndex(mt => new { mt.UserId, mt.Name })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");
            });

            // تنظیمات ContactTag
            modelBuilder.Entity<ContactTag>(entity =>
            {
                entity.HasKey(ct => ct.Id);
                entity.Property(ct => ct.Id).ValueGeneratedOnAdd();

                entity.Property(ct => ct.ContactId).IsRequired();
                entity.Property(ct => ct.TagId).IsRequired();
                entity.Property(ct => ct.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(ct => ct.Contact)
                    .WithMany(c => c.ContactTags)
                    .HasForeignKey(ct => ct.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ct => ct.Tag)
                    .WithMany(t => t.ContactTags)
                    .HasForeignKey(ct => ct.TagId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ct => ct.ContactId);
                entity.HasIndex(ct => ct.TagId);
                entity.HasIndex(ct => new { ct.ContactId, ct.TagId })
                    .IsUnique();
            });

            // تنظیمات WalletTransaction
            modelBuilder.Entity<WalletTransaction>(entity =>
            {
                entity.HasKey(wt => wt.Id);
                entity.Property(wt => wt.Id).ValueGeneratedOnAdd();

                entity.Property(wt => wt.UserId).IsRequired();
                entity.Property(wt => wt.TransactionType).IsRequired().HasMaxLength(50);
                entity.Property(wt => wt.Title).IsRequired().HasMaxLength(200);
                entity.Property(wt => wt.Description).HasMaxLength(500);
                entity.Property(wt => wt.ReferenceNumber).HasMaxLength(100);
                entity.Property(wt => wt.Status).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(wt => wt.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // تنظیم Precision برای فیلدهای decimal
                entity.Property(wt => wt.Amount).HasPrecision(18, 2);
                entity.Property(wt => wt.BalanceBefore).HasPrecision(18, 2);
                entity.Property(wt => wt.BalanceAfter).HasPrecision(18, 2);

                entity.HasOne(wt => wt.User)
                    .WithMany()
                    .HasForeignKey(wt => wt.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(wt => wt.Payment)
                    .WithMany(p => p.WalletTransactions)
                    .HasForeignKey(wt => wt.PaymentId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(wt => wt.Cashback)
                    .WithMany(c => c.WalletTransactions)
                    .HasForeignKey(wt => wt.CashbackId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(wt => wt.UserId);
                entity.HasIndex(wt => wt.TransactionType);
                entity.HasIndex(wt => wt.Status);
                entity.HasIndex(wt => wt.CreatedAt);
            });

            // تنظیمات Payment
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedOnAdd();

                entity.Property(p => p.UserId).IsRequired();
                entity.Property(p => p.PaymentType).IsRequired().HasMaxLength(50);
                entity.Property(p => p.Gateway).IsRequired().HasMaxLength(50);
                entity.Property(p => p.OrderId).IsRequired().HasMaxLength(100);
                entity.Property(p => p.RefId).HasMaxLength(100);
                entity.Property(p => p.ReferenceNumber).HasMaxLength(100);
                entity.Property(p => p.TransactionId).HasMaxLength(100);
                entity.Property(p => p.CardNumber).HasMaxLength(20);
                entity.Property(p => p.Status).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(p => p.ErrorCode).HasMaxLength(50);
                entity.Property(p => p.ErrorMessage).HasMaxLength(500);
                entity.Property(p => p.Description).HasMaxLength(500);
                entity.Property(p => p.IpAddress).HasMaxLength(50);
                entity.Property(p => p.UserAgent).HasMaxLength(500);
                entity.Property(p => p.CallbackUrl).HasMaxLength(500);
                entity.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // تنظیم Precision برای فیلد decimal
                entity.Property(p => p.Amount).HasPrecision(18, 2);

                entity.HasOne(p => p.User)
                    .WithMany()
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(p => p.UserId);
                entity.HasIndex(p => p.OrderId).IsUnique();
                entity.HasIndex(p => p.Status);
                entity.HasIndex(p => p.Gateway);
                entity.HasIndex(p => p.CreatedAt);
            });

            // تنظیمات Cashback
            modelBuilder.Entity<Cashback>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).ValueGeneratedOnAdd();

                entity.Property(c => c.UserId).IsRequired();
                entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
                entity.Property(c => c.Description).HasMaxLength(500);
                entity.Property(c => c.CashbackType).IsRequired().HasMaxLength(50).HasDefaultValue("Percentage");
                entity.Property(c => c.DepositTiming).HasMaxLength(50).HasDefaultValue("Immediate");
                entity.Property(c => c.TargetAudience).HasMaxLength(50).HasDefaultValue("All");
                entity.Property(c => c.TargetNotebookIds).HasMaxLength(1000);
                entity.Property(c => c.TargetTagIds).HasMaxLength(1000);
                entity.Property(c => c.SendToSpecificTags).HasDefaultValue(false);
                entity.Property(c => c.IsActive).HasDefaultValue(true);
                entity.Property(c => c.IsDeleted).HasDefaultValue(false);
                entity.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // تنظیم Precision برای فیلدهای decimal
                entity.Property(c => c.Percentage).HasPrecision(5, 2);
                entity.Property(c => c.FixedAmount).HasPrecision(18, 2);
                entity.Property(c => c.MaxCashbackAmount).HasPrecision(18, 2);
                entity.Property(c => c.MinPurchaseAmount).HasPrecision(18, 2);

                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(c => c.UserId);
                entity.HasIndex(c => c.IsActive);
                entity.HasIndex(c => c.IsDeleted);
                entity.HasIndex(c => c.StartDate);
                entity.HasIndex(c => c.EndDate);
            });

            // تنظیمات CashbackTransaction
            modelBuilder.Entity<CashbackTransaction>(entity =>
            {
                entity.HasKey(ct => ct.Id);
                entity.Property(ct => ct.Id).ValueGeneratedOnAdd();

                entity.Property(ct => ct.CashbackId).IsRequired();
                entity.Property(ct => ct.ContactId).IsRequired();
                entity.Property(ct => ct.Status).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(ct => ct.Description).HasMaxLength(500);
                entity.Property(ct => ct.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // تنظیم Precision برای فیلدهای decimal
                entity.Property(ct => ct.Amount).HasPrecision(18, 2);
                entity.Property(ct => ct.PurchaseAmount).HasPrecision(18, 2);

                entity.HasOne(ct => ct.Cashback)
                    .WithMany(c => c.CashbackTransactions)
                    .HasForeignKey(ct => ct.CashbackId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(ct => ct.Contact)
                    .WithMany()
                    .HasForeignKey(ct => ct.ContactId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(ct => ct.CashbackId);
                entity.HasIndex(ct => ct.ContactId);
                entity.HasIndex(ct => ct.Status);
                entity.HasIndex(ct => ct.CreatedAt);
            });

            // تنظیمات CashbackDraft
            modelBuilder.Entity<CashbackDraft>(entity =>
            {
                entity.HasKey(cd => cd.Id);
                entity.Property(cd => cd.Id).ValueGeneratedOnAdd();

                entity.Property(cd => cd.UserId).IsRequired();
                entity.Property(cd => cd.DraftId).IsRequired().HasMaxLength(100);
                entity.Property(cd => cd.Step1Data).IsRequired();
                entity.Property(cd => cd.Step3Data).IsRequired(false); // اختیاری
                entity.Property(cd => cd.IsDeleted).HasDefaultValue(false);
                entity.Property(cd => cd.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(cd => cd.User)
                    .WithMany()
                    .HasForeignKey(cd => cd.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(cd => cd.UserId);
                entity.HasIndex(cd => cd.DraftId).IsUnique();
                entity.HasIndex(cd => cd.ExpiresAt);
                entity.HasIndex(cd => cd.IsDeleted);
            });

            // تنظیمات UserNotificationSettings
            modelBuilder.Entity<UserNotificationSettings>(entity =>
            {
                entity.HasKey(uns => uns.Id);
                entity.Property(uns => uns.Id).ValueGeneratedOnAdd();

                entity.Property(uns => uns.UserId)
                    .IsRequired();

                // مقادیر پیش‌فرض برای اعلان‌های سیستمی
                entity.Property(uns => uns.ImportantNotifications)
                    .HasDefaultValue(true);
                entity.Property(uns => uns.Updates)
                    .HasDefaultValue(false);
                entity.Property(uns => uns.SystemWarnings)
                    .HasDefaultValue(true);

                // مقادیر پیش‌فرض برای اعلان‌های مالی
                entity.Property(uns => uns.WalletTransaction)
                    .HasDefaultValue(false);
                entity.Property(uns => uns.CustomerCashback)
                    .HasDefaultValue(true);
                entity.Property(uns => uns.FinancialReport)
                    .HasDefaultValue(false);

                // مقادیر پیش‌فرض برای اعلان‌های متفرقه
                entity.Property(uns => uns.NewCustomerRegistration)
                    .HasDefaultValue(false);
                entity.Property(uns => uns.Suggestions)
                    .HasDefaultValue(true);
                entity.Property(uns => uns.EducationAndTips)
                    .HasDefaultValue(false);

                entity.Property(uns => uns.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // رابطه با User
                entity.HasOne(uns => uns.User)
                    .WithMany()
                    .HasForeignKey(uns => uns.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ایندکس‌ها
                entity.HasIndex(uns => uns.UserId).IsUnique();
            });

            // تنظیمات ContactCashbackBalance
            modelBuilder.Entity<ContactCashbackBalance>(entity =>
            {
                entity.HasKey(ccb => ccb.Id);
                entity.Property(ccb => ccb.Id).ValueGeneratedOnAdd();

                entity.Property(ccb => ccb.ContactId).IsRequired();
                entity.Property(ccb => ccb.UserId).IsRequired();
                entity.Property(ccb => ccb.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // تنظیم Precision برای فیلدهای decimal
                entity.Property(ccb => ccb.TotalBalance).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(ccb => ccb.UsableBalance).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(ccb => ccb.ActiveCashbackPercentage).HasPrecision(5, 2);

                // تنظیم RowVersion برای Optimistic Concurrency Control
                entity.Property(ccb => ccb.RowVersion).IsRowVersion();

                // رابطه با Contact
                entity.HasOne(ccb => ccb.Contact)
                    .WithMany()
                    .HasForeignKey(ccb => ccb.ContactId)
                    .OnDelete(DeleteBehavior.NoAction);

                // رابطه با User
                entity.HasOne(ccb => ccb.User)
                    .WithMany()
                    .HasForeignKey(ccb => ccb.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                // ایندکس‌ها
                entity.HasIndex(ccb => ccb.ContactId);
                entity.HasIndex(ccb => ccb.UserId);
                // ایندکس یکتا برای جلوگیری از ایجاد چند رکورد برای یک مخاطب
                entity.HasIndex(ccb => new { ccb.ContactId, ccb.UserId }).IsUnique();
            });

            // تنظیمات ManualCashbackTransaction
            modelBuilder.Entity<ManualCashbackTransaction>(entity =>
            {
                entity.HasKey(mct => mct.Id);
                entity.Property(mct => mct.Id).ValueGeneratedOnAdd();

                entity.Property(mct => mct.ContactId).IsRequired();
                entity.Property(mct => mct.UserId).IsRequired();
                entity.Property(mct => mct.TransactionType).IsRequired().HasMaxLength(50).HasDefaultValue("Add");
                entity.Property(mct => mct.Description).HasMaxLength(500);
                entity.Property(mct => mct.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // تنظیم Precision برای فیلدهای decimal
                entity.Property(mct => mct.Amount).HasPrecision(18, 2);
                entity.Property(mct => mct.BalanceBefore).HasPrecision(18, 2);
                entity.Property(mct => mct.BalanceAfter).HasPrecision(18, 2);

                // رابطه با Contact
                entity.HasOne(mct => mct.Contact)
                    .WithMany()
                    .HasForeignKey(mct => mct.ContactId)
                    .OnDelete(DeleteBehavior.NoAction);

                // رابطه با User
                entity.HasOne(mct => mct.User)
                    .WithMany()
                    .HasForeignKey(mct => mct.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                // ایندکس‌ها
                entity.HasIndex(mct => mct.ContactId);
                entity.HasIndex(mct => mct.UserId);
                entity.HasIndex(mct => mct.TransactionType);
                entity.HasIndex(mct => mct.CreatedAt);
            });

            // تنظیمات SubscriptionPlan
            modelBuilder.Entity<SubscriptionPlan>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedOnAdd();
                entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
                entity.Property(p => p.TierCode).IsRequired().HasMaxLength(50);
                entity.Property(p => p.Description).HasMaxLength(1000);
                entity.Property(p => p.Price).HasPrecision(18, 2);
                entity.Property(p => p.IsActive).HasDefaultValue(true);
                entity.Property(p => p.IsDeleted).HasDefaultValue(false);
                entity.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasIndex(p => p.TierCode);
                entity.HasIndex(p => p.IsActive);
            });

            // تنظیمات UserSubscription
            modelBuilder.Entity<UserSubscription>(entity =>
            {
                entity.HasKey(us => us.Id);
                entity.Property(us => us.Id).ValueGeneratedOnAdd();
                entity.Property(us => us.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Active");
                entity.Property(us => us.IsDeleted).HasDefaultValue(false);
                entity.Property(us => us.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(us => us.User).WithMany().HasForeignKey(us => us.UserId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(us => us.Plan).WithMany(p => p.UserSubscriptions).HasForeignKey(us => us.SubscriptionPlanId).OnDelete(DeleteBehavior.NoAction);
                entity.HasIndex(us => us.UserId);
                entity.HasIndex(us => us.Status);
            });

            // تنظیمات SupportTicket
            modelBuilder.Entity<SupportTicket>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Id).ValueGeneratedOnAdd();
                entity.Property(t => t.Subject).IsRequired().HasMaxLength(300);
                entity.Property(t => t.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Open");
                entity.Property(t => t.Priority).IsRequired().HasMaxLength(50).HasDefaultValue("Normal");
                entity.Property(t => t.IsDeleted).HasDefaultValue(false);
                entity.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(t => t.AssignedToUser).WithMany().HasForeignKey(t => t.AssignedToUserId).OnDelete(DeleteBehavior.NoAction);
                entity.HasIndex(t => t.UserId);
                entity.HasIndex(t => t.Status);
            });

            // تنظیمات TicketMessage
            modelBuilder.Entity<TicketMessage>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Id).ValueGeneratedOnAdd();
                entity.Property(m => m.Content).IsRequired().HasMaxLength(4000);
                entity.Property(m => m.IsDeleted).HasDefaultValue(false);
                entity.Property(m => m.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(m => m.Ticket).WithMany(t => t.Messages).HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(m => m.SenderUser).WithMany().HasForeignKey(m => m.SenderUserId).OnDelete(DeleteBehavior.NoAction);
                entity.HasIndex(m => m.TicketId);
            });

            // تنظیمات EducationalVideo
            modelBuilder.Entity<EducationalVideo>(entity =>
            {
                entity.HasKey(v => v.Id);
                entity.Property(v => v.Id).ValueGeneratedOnAdd();
                entity.Property(v => v.Title).IsRequired().HasMaxLength(300);
                entity.Property(v => v.Description).HasMaxLength(1000);
                entity.Property(v => v.VideoUrl).IsRequired().HasMaxLength(1000);
                entity.Property(v => v.ThumbnailUrl).HasMaxLength(1000);
                entity.Property(v => v.IsActive).HasDefaultValue(true);
                entity.Property(v => v.IsDeleted).HasDefaultValue(false);
                entity.Property(v => v.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasIndex(v => v.IsActive);
                entity.HasIndex(v => v.SortOrder);
            });

            // تنظیمات SmsApprovalRequest
            modelBuilder.Entity<SmsApprovalRequest>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).ValueGeneratedOnAdd();
                entity.Property(r => r.RequestType).IsRequired().HasMaxLength(50);
                entity.Property(r => r.ContentPreview).IsRequired().HasMaxLength(4000);
                entity.Property(r => r.TitlePreview).HasMaxLength(300);
                entity.Property(r => r.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(r => r.RejectionReason).HasMaxLength(1000);
                entity.Property(r => r.IsDeleted).HasDefaultValue(false);
                entity.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(r => r.MessageCampaign).WithMany().HasForeignKey(r => r.MessageCampaignId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(r => r.Message).WithMany().HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(r => r.MessageSession).WithMany().HasForeignKey(r => r.MessageSessionId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(r => r.ReviewedByUser).WithMany().HasForeignKey(r => r.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
                entity.HasIndex(r => r.Status);
                entity.HasIndex(r => r.UserId);
                entity.HasIndex(r => r.MessageCampaignId);
            });

            // فیلدهای تأیید ادمین روی MessageTemplate
            modelBuilder.Entity<MessageTemplate>(entity =>
            {
                entity.Property(t => t.ApprovalStatus).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(t => t.RejectionReason).HasMaxLength(1000);
            });

            // فیلدهای تأیید ادمین روی MessageCampaign
            modelBuilder.Entity<MessageCampaign>(entity =>
            {
                entity.Property(c => c.AdminApprovalStatus).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(c => c.AdminRejectionReason).HasMaxLength(1000);
            });
        }
    }
}
