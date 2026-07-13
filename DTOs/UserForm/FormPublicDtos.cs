using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.UserForm
{
    public class FormPublicDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Slug { get; set; } = string.Empty;

        public string? TemplateKey { get; set; }

        public List<FormPublicFieldDto> Fields { get; set; } = new();
    }

    public class FormPublicFieldDto
    {
        public string FieldKey { get; set; } = string.Empty;

        public string FieldType { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string? Placeholder { get; set; }

        public string? HelpText { get; set; }

        public bool IsRequired { get; set; }
    }

    public class SubmitFormPublicDto
    {
        [Required]
        public string ParticipantFullName { get; set; } = string.Empty;

        [Required]
        public string ParticipantMobile { get; set; } = string.Empty;

        public Dictionary<string, string?> Values { get; set; } = new();
    }

    public class SubmitFormPublicResponseDto
    {
        public int SubmissionId { get; set; }
    }
}
