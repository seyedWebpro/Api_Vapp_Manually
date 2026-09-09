using System.Text.Json;
using Api_Vapp.DTOs.Automation;
using Xunit;

namespace Api_Vapp.Tests.Automation;

public class AutomatedMessageRecipientContractTests
{
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
}
