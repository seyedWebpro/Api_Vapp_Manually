using Api_Vapp.Constants;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.Automation
{
    public class OccasionGreetingPlannerTests
    {
        private const int OwnerId = 6;
        private const int OtherUserId = 2;

        private static SpecialOccasion Custom(int id, int ownerId, string category = OccasionCategories.Congratulation) => new()
        {
            Id = id,
            UserId = ownerId,
            Name = $"مناسبت {id}",
            Category = category,
            CalendarType = OccasionCalendarTypes.Jalali,
            Month = 7,
            Day = 7,
            IsSystem = false,
            IsActive = true
        };

        private static SpecialOccasion System(int id, string? defaultMessage = "{{نام}} عزیز، {{مناسبت}} مبارک — {{نام برند}}") => new()
        {
            Id = id,
            UserId = null,
            Name = "جشن مهرگان",
            Category = OccasionCategories.Congratulation,
            CalendarType = OccasionCalendarTypes.Jalali,
            Month = 7,
            Day = 7,
            DefaultMessage = defaultMessage,
            IsSystem = true,
            IsActive = true
        };

        private static UserOccasionPreference Pref(
            int userId,
            int occasionId,
            bool enabled = true,
            string? customMessage = "سلام {{نام}}",
            string approval = AdminApprovalStatuses.Approved) => new()
        {
            UserId = userId,
            SpecialOccasionId = occasionId,
            IsEnabled = enabled,
            ApplyToAllContacts = true,
            CustomMessage = customMessage,
            TemplateApprovalStatus = approval
        };

        private static Contact ContactOf(int id, int notebookId = 1, string mobile = "09120000000") => new()
        {
            Id = id,
            ContactNotebookId = notebookId,
            MobileNumber = mobile,
            FullName = $"مخاطب {id}"
        };

        private static readonly IReadOnlySet<(int, int)> NoneHandled = new HashSet<(int, int)>();

        [Fact]
        public void ResolveEnabledOccasions_CustomOccasionWithEnabledPreference_IsIncludedWithoutAutomatedMessage()
        {
            var occasion = Custom(1040, OwnerId);

            var result = OccasionGreetingPlanner.ResolveEnabledOccasions([occasion], [Pref(OwnerId, 1040)]);

            var item = Assert.Single(result);
            Assert.Equal(OwnerId, item.UserId);
            Assert.Equal(1040, item.Occasion.Id);
        }

        [Fact]
        public void ResolveEnabledOccasions_DisabledPreference_IsExcluded()
        {
            var result = OccasionGreetingPlanner.ResolveEnabledOccasions(
                [Custom(1037, OtherUserId)],
                [Pref(OtherUserId, 1037, enabled: false)]);

            Assert.Empty(result);
        }

        [Fact]
        public void ResolveEnabledOccasions_SystemOccasionWithoutPreference_IsOptInAndExcluded()
        {
            var result = OccasionGreetingPlanner.ResolveEnabledOccasions([System(3)], []);

            Assert.Empty(result);
        }

        [Fact]
        public void ResolveEnabledOccasions_SystemOccasion_IncludedPerUserWhoEnabledIt()
        {
            var result = OccasionGreetingPlanner.ResolveEnabledOccasions(
                [System(3)],
                [Pref(OwnerId, 3, customMessage: null), Pref(OtherUserId, 3, enabled: false, customMessage: null)]);

            var item = Assert.Single(result);
            Assert.Equal(OwnerId, item.UserId);
        }

        [Fact]
        public void ResolveEnabledOccasions_PreferenceOnAnotherUsersCustomOccasion_IsIgnored()
        {
            var result = OccasionGreetingPlanner.ResolveEnabledOccasions(
                [Custom(1040, OwnerId)],
                [Pref(OtherUserId, 1040)]);

            Assert.Single(result);
            Assert.Equal(OwnerId, result[0].UserId);
            Assert.Null(result[0].Preference);
        }

        [Fact]
        public void ResolveEnabledOccasions_DeletedPreference_TreatedAsMissing()
        {
            var deleted = Pref(OwnerId, 1040, enabled: false);
            deleted.IsDeleted = true;

            var result = OccasionGreetingPlanner.ResolveEnabledOccasions([Custom(1040, OwnerId)], [deleted]);

            var item = Assert.Single(result);
            Assert.Null(item.Preference);
        }

        [Fact]
        public void PlanUserBatches_SendsApprovedCustomTemplateToAllContacts()
        {
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions(
                [Custom(1040, OwnerId)], [Pref(OwnerId, 1040, customMessage: "{{نام}} عزیز، {{مناسبت}} مبارک")]);

            var (batches, skipped) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId, enabled, profile: null, [ContactOf(1), ContactOf(2)], NoneHandled);

            Assert.Empty(skipped);
            var batch = Assert.Single(batches);
            Assert.Equal(2, batch.Contacts.Count);
            Assert.Equal("{{نام}} عزیز، مناسبت 1040 مبارک", batch.Content);
        }

        [Fact]
        public void PlanUserBatches_AppliesBusinessNameFromProfile()
        {
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions([System(3)], [Pref(OwnerId, 3, customMessage: null)]);
            var profile = new UserOccasionProfile { UserId = OwnerId, BusinessName = "هلدینگ" };

            var (batches, _) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId, enabled, profile, [ContactOf(1)], NoneHandled);

            Assert.Equal("{{نام}} عزیز، جشن مهرگان مبارک — هلدینگ", Assert.Single(batches).Content);
        }

        [Fact]
        public void PlanUserBatches_PendingCustomTemplate_IsSkippedAsEmptyTemplate()
        {
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions(
                [Custom(1040, OwnerId)], [Pref(OwnerId, 1040, approval: AdminApprovalStatuses.Pending)]);

            var (batches, skipped) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId, enabled, null, [ContactOf(1)], NoneHandled);

            Assert.Empty(batches);
            Assert.Equal(OccasionGreetingPlanner.SkipReasonEmptyTemplate, Assert.Single(skipped).Reason);
        }

        [Fact]
        public void PlanUserBatches_DisabledCategory_IsSkipped()
        {
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions(
                [Custom(50, OwnerId, OccasionCategories.Condolence)], [Pref(OwnerId, 50)]);
            var profile = new UserOccasionProfile { UserId = OwnerId, CondolencesEnabled = false };

            var (batches, skipped) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId, enabled, profile, [ContactOf(1)], NoneHandled);

            Assert.Empty(batches);
            Assert.Equal(OccasionGreetingPlanner.SkipReasonCategoryDisabled, Assert.Single(skipped).Reason);
        }

        [Fact]
        public void PlanUserBatches_AlreadyHandledContactsToday_AreNotResent()
        {
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions([Custom(1040, OwnerId)], [Pref(OwnerId, 1040)]);
            var handled = new HashSet<(int, int)> { (1040, 1) };

            var (batches, _) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId, enabled, null, [ContactOf(1), ContactOf(2)], handled);

            var batch = Assert.Single(batches);
            Assert.Equal(2, Assert.Single(batch.Contacts).Id);
        }

        [Fact]
        public void PlanUserBatches_AllContactsHandled_ProducesNoBatch()
        {
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions([Custom(1040, OwnerId)], [Pref(OwnerId, 1040)]);
            var handled = new HashSet<(int, int)> { (1040, 1) };

            var (batches, skipped) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId, enabled, null, [ContactOf(1)], handled);

            Assert.Empty(batches);
            Assert.Equal(OccasionGreetingPlanner.SkipReasonNoRecipients, Assert.Single(skipped).Reason);
        }

        [Fact]
        public void PlanUserBatches_RespectsPerOccasionAudienceAndExclusions()
        {
            var pref = Pref(OwnerId, 1040);
            pref.ApplyToAllContacts = false;
            pref.ContactNotebookIdsJson = "[10]";
            pref.ExcludedContactIdsJson = "[2]";
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions([Custom(1040, OwnerId)], [pref]);

            var (batches, _) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId,
                enabled,
                null,
                [ContactOf(1, notebookId: 10), ContactOf(2, notebookId: 10), ContactOf(3, notebookId: 11)],
                NoneHandled);

            Assert.Equal([1], Assert.Single(batches).Contacts.Select(c => c.Id));
        }

        [Fact]
        public void PlanUserBatches_ContactsWithoutMobile_AreIgnored()
        {
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions([Custom(1040, OwnerId)], [Pref(OwnerId, 1040)]);

            var (batches, _) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId, enabled, null, [ContactOf(1), ContactOf(2, mobile: " ")], NoneHandled);

            Assert.Equal([1], Assert.Single(batches).Contacts.Select(c => c.Id));
        }

        [Fact]
        public void PlanUserBatches_OnlyPlansOccasionsOfRequestedUser()
        {
            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions(
                [Custom(1040, OwnerId), Custom(1036, OtherUserId)],
                [Pref(OwnerId, 1040), Pref(OtherUserId, 1036)]);

            var (batches, _) = OccasionGreetingPlanner.PlanUserBatches(
                OwnerId, enabled, null, [ContactOf(1)], NoneHandled);

            Assert.Equal(1040, Assert.Single(batches).Occasion.Id);
        }

        [Fact]
        public void ResolveSendTimeTehran_DefaultsToTenWhenProfileMissing()
        {
            Assert.Equal(new TimeSpan(10, 0, 0), OccasionGreetingPlanner.ResolveSendTimeTehran(null));
            Assert.Equal(
                new TimeSpan(12, 7, 0),
                OccasionGreetingPlanner.ResolveSendTimeTehran(new UserOccasionProfile { ScheduledTimeTehran = new TimeSpan(12, 7, 0) }));
        }
    }
}
