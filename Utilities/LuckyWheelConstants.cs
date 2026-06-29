namespace Api_Vapp.Utilities
{
    public static class LuckyWheelConstants
    {
        public const int MinItems = 2;
        public const int MaxItems = 8;
        public const int MaxSlugLength = 100;
        public const int SlugGenerationMaxAttempts = 100;
        public const decimal RequiredProbabilityTotal = 100m;
    }
}
