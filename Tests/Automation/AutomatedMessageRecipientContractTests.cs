using System.Text.Json;
using Api_Vapp.DTOs.Automation;
using Xunit;

namespace Api_Vapp.Tests.Automation;

public class AutomatedMessageRecipientContractTests
{
    [Fact]
    public void ApplyToAllRequest_DoesNotRequireNotebookIds()
    {
        var request = new SelectRecipientsForAutomatedMessageDto
        {
            ApplyToAllContacts = true,
            ContactNotebookIds = null,
            ContactNotebookId = null,
            ExcludedContactIds = null
        };

        Assert.True(request.ApplyToAllContacts);
        Assert.Null(request.ContactNotebookIds);
        Assert.Null(request.ContactNotebookId);
    }

    [Fact]
    public void DefaultRecipient_SerializesRequiredMobileFieldsWithoutNulls()
    {
        var recipient = new RecipientItemForAutomatedMessageDto();

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(recipient));
        var root = json.RootElement;

        Assert.Equal(0, root.GetProperty(nameof(recipient.ContactId)).GetInt32());
        Assert.Equal(string.Empty, root.GetProperty(nameof(recipient.MobileNumber)).GetString());
        Assert.Equal(string.Empty, root.GetProperty(nameof(recipient.FullName)).GetString());
    }

    [Fact]
    public void ApplyToAllResponse_WithUnnamedContact_SerializesEmptyFullName()
    {
        var response = new RecipientListForAutomatedMessageResponseDto
        {
            Recipients =
            [
                new RecipientItemForAutomatedMessageDto
                {
                    ContactId = 42,
                    MobileNumber = "09120000000",
                    FullName = string.Empty,
                    IsEligible = true
                }
            ],
            TotalCount = 1,
            EligibleCount = 1
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var recipient = json.RootElement.GetProperty(nameof(response.Recipients))[0];

        Assert.Equal(string.Empty, recipient.GetProperty(nameof(RecipientItemForAutomatedMessageDto.FullName)).GetString());
        Assert.Equal(42, recipient.GetProperty(nameof(RecipientItemForAutomatedMessageDto.ContactId)).GetInt32());
    }
}
