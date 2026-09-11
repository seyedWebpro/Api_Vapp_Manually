namespace Api_Vapp.Constants
{
    public static class ForbiddenWordCacheKeys
    {
        public const string ActiveNormalizedList = "ForbiddenWords:ActiveNormalized";
    }

    public static class ForbiddenWordLimits
    {
        public const int MaxWordLength = 100;
        public const int MaxBulkCreate = 50;
    }
}
