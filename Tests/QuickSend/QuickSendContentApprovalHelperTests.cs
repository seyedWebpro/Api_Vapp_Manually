using Api_Vapp.Constants;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.QuickSend
{
    public class QuickSendContentApprovalHelperTests
    {
        private sealed class FakeApprovable : IQuickSendApprovable
        {
            public string ApprovalStatus { get; set; } = AdminApprovalStatuses.Approved;
            public DateTime? ApprovedAt { get; set; } = DateTime.UtcNow;
            public int? ApprovedByUserId { get; set; } = 1;
            public string? RejectionReason { get; set; } = "old";
            public DateTime? UpdatedAt { get; set; }
        }

        [Fact]
        public void ResetToPending_ClearsPriorReview()
        {
            var entity = new FakeApprovable();
            QuickSendContentApprovalHelper.ResetToPending(entity);

            Assert.Equal(AdminApprovalStatuses.Pending, entity.ApprovalStatus);
            Assert.Null(entity.ApprovedAt);
            Assert.Null(entity.ApprovedByUserId);
            Assert.Null(entity.RejectionReason);
            Assert.NotNull(entity.UpdatedAt);
        }

        [Fact]
        public void ResetToPendingIfNeeded_SkipsWhenContentUnchanged()
        {
            var entity = new FakeApprovable();
            QuickSendContentApprovalHelper.ResetToPendingIfNeeded(entity, contentChanged: false);
            Assert.Equal(AdminApprovalStatuses.Approved, entity.ApprovalStatus);
        }

        [Fact]
        public void TryBlockIfNotApproved_Returns202ForPending()
        {
            var result = QuickSendContentApprovalHelper.TryBlockIfNotApproved(
                AdminApprovalStatuses.Pending,
                null,
                "کارت ویزیت");

            Assert.NotNull(result);
            Assert.Equal(202, result!.StatusCode);
            Assert.True(result.Success);
            Assert.Equal(AdminApprovalStatuses.Pending, result.Data!.AdminApprovalStatus);
            Assert.Contains("صف تأیید", result.Message);
            Assert.Contains("بدون نیاز به تأیید مجدد", result.Message);
        }

        [Fact]
        public void TryBlockIfNotApproved_Returns400ForRejected()
        {
            var result = QuickSendContentApprovalHelper.TryBlockIfNotApproved(
                AdminApprovalStatuses.Rejected,
                "محتوای نامناسب",
                "فرم");

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
            Assert.False(result.Success);
            Assert.Contains("رد شده", result.Message);
            Assert.Contains("محتوای نامناسب", result.Message);
        }

        [Fact]
        public void TryBlockIfNotApproved_ReturnsNullForApproved()
        {
            var result = QuickSendContentApprovalHelper.TryBlockIfNotApproved(
                AdminApprovalStatuses.Approved,
                null,
                "رزرو نوبت");

            Assert.Null(result);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("Foo", false)]
        [InlineData("BusinessCard", true)]
        [InlineData("businesscard", true)]
        public void QuickSendItemTypes_IsValid(string? itemType, bool expected)
        {
            Assert.Equal(expected, QuickSendItemTypes.IsValid(itemType));
        }
    }
}
