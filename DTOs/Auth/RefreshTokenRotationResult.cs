namespace Api_Vapp.DTOs.Auth
{
    public enum RefreshTokenRotationStatus
    {
        Rotated = 1,
        GraceReuse = 2,
        Invalid = 3,
        InactiveUser = 4
    }

    public sealed class RefreshTokenRotationResult
    {
        public RefreshTokenRotationStatus Status { get; init; }
        public global::Api_Vapp.Models.RefreshToken? RefreshToken { get; init; }
        public global::Api_Vapp.Models.User? User { get; init; }

        public static RefreshTokenRotationResult Rotated(
            global::Api_Vapp.Models.RefreshToken token,
            global::Api_Vapp.Models.User user) => new()
        {
            Status = RefreshTokenRotationStatus.Rotated,
            RefreshToken = token,
            User = user
        };

        public static RefreshTokenRotationResult GraceReuse(
            global::Api_Vapp.Models.RefreshToken token,
            global::Api_Vapp.Models.User user) => new()
        {
            Status = RefreshTokenRotationStatus.GraceReuse,
            RefreshToken = token,
            User = user
        };

        public static RefreshTokenRotationResult Invalid() => new()
        {
            Status = RefreshTokenRotationStatus.Invalid
        };

        public static RefreshTokenRotationResult InactiveUser() => new()
        {
            Status = RefreshTokenRotationStatus.InactiveUser
        };
    }
}
