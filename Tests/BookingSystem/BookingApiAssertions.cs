using Api_Vapp.DTOs.Common;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.BookingSystem;

/// <summary>
/// Assertionهای مشترک — اطمینان از پاسخ‌های کنترل‌شده (بدون 500 یا پیام فنی)
/// </summary>
internal static class BookingApiAssertions
{
    public static void AssertManagedResponse<T>(ApiResponse<T> response, int expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        AssertNoServerLeak(response);
    }

    public static void AssertSuccess<T>(ApiResponse<T> response, int expectedStatusCode = 200)
    {
        Assert.True(response.Success, $"Expected success but got: {response.Message}");
        AssertManagedResponse(response, expectedStatusCode);
    }

    public static void AssertFailure<T>(ApiResponse<T> response, int expectedStatusCode)
    {
        Assert.False(response.Success, $"Expected failure but got success with: {response.Message}");
        AssertManagedResponse(response, expectedStatusCode);
        Assert.True(
            ControlledErrorHelper.IsSafeUserMessage(response.Message),
            $"Unsafe error message: {response.Message}");
    }

    public static void AssertNoServerLeak<T>(ApiResponse<T> response)
    {
        Assert.NotEqual(500, response.StatusCode);

        if (!response.Success || response.StatusCode >= 400)
        {
            Assert.False(
                string.Equals(response.Message, ControlledErrorHelper.Unexpected, StringComparison.Ordinal) &&
                response.StatusCode is not (500 or 503),
                "Unexpected 500 message on non-server error status");

            if (response.StatusCode == 500)
            {
                Assert.Equal(ControlledErrorHelper.Unexpected, response.Message);
            }
            else if (!response.Success)
            {
                Assert.True(
                    ControlledErrorHelper.IsSafeUserMessage(response.Message),
                    $"Unsafe error message at status {response.StatusCode}: {response.Message}");
            }
        }

        if (response.Errors != null)
        {
            foreach (var error in response.Errors)
            {
                Assert.True(
                    ControlledErrorHelper.IsSafeUserMessage(error),
                    $"Unsafe error in errors[]: {error}");
            }
        }
    }
}
