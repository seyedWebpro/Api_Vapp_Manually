using Api_Vapp.DTOs.Common;
using Xunit;

namespace Api_Vapp.Tests.Shared;

internal static class ApiTestAssertions
{
    public static void AssertControlledResponse<T>(ApiResponse<T> result)
    {
        Assert.NotEqual(500, result.StatusCode);
        Assert.NotEqual(ErrorCodes.Unexpected, result.ErrorCode);
        Assert.NotEqual(ErrorCodes.DatabaseError, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.DoesNotContain("Exception", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlException", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static void AssertSuccess<T>(ApiResponse<T> result, int expectedStatus = 200)
    {
        AssertControlledResponse(result);
        Assert.True(result.Success);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.NotNull(result.Data);
    }

    public static void AssertClientError<T>(ApiResponse<T> result, int expectedStatus = 400)
    {
        AssertControlledResponse(result);
        Assert.False(result.Success);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.NotNull(result.ErrorCode);
    }
}
