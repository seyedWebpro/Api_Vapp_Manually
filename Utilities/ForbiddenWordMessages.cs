using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Utilities
{
    /// <summary>پیام‌ها و پاسخ‌های کنترل‌شده برای کلمات فیلتر.</summary>
    public static class ForbiddenWordMessages
    {
        public static string BuildUserMessage(IReadOnlyList<string> matchedWords)
        {
            if (matchedWords == null || matchedWords.Count == 0)
                return "متن شامل کلمه فیلتر شده است و امکان ادامه وجود ندارد.";

            if (matchedWords.Count == 1)
            {
                return $"کلمه «{matchedWords[0]}» فیلتر است و نمی‌توانید از این کلمه استفاده کنید. لطفاً آن را حذف کنید.";
            }

            var joined = string.Join("، ", matchedWords.Select(w => $"«{w}»"));
            return $"کلمات فیلتر شده: {joined}. لطفاً آن‌ها را حذف کنید؛ تا زمان حذف امکان ادامه فرایند وجود ندارد.";
        }

        public static ApiResponse<T> BlockedResponse<T>(IReadOnlyList<string> matchedWords) =>
            ApiResponse<T>.BadRequest(
                BuildUserMessage(matchedWords),
                matchedWords.ToList(),
                ErrorCodes.FilteredWord);
    }
}
