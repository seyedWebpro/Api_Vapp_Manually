namespace Api_Vapp.DTOs.Zohal
{
    public sealed class ShahkarVerifyContext
    {
        public string Source { get; set; } = "register";

        public string? IpAddress { get; set; }

        public string? TraceId { get; set; }
    }
}
