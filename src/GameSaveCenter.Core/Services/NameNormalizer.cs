using System.Globalization;
using System.Text;

namespace GameSaveCenter.Core.Services
{
    /// <summary>Produces stable comparison keys for game title matching.</summary>
    public static class NameNormalizer
    {
        /// <summary>
        /// Removes punctuation, diacritics and common edition words while preserving
        /// letters and digits. The result is intentionally conservative; platform IDs
        /// always take precedence when available.
        /// </summary>
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = value.Normalize(NormalizationForm.FormD).ToLowerInvariant();
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return RemoveEditionSuffixes(builder.ToString());
        }

        private static string RemoveEditionSuffixes(string normalized)
        {
            var suffixes = new[]
            {
                "gameoftheyearedition", "definitiveedition", "completeedition",
                "ultimateedition", "specialedition", "remastered", "remake"
            };

            foreach (var suffix in suffixes)
            {
                if (normalized.EndsWith(suffix, System.StringComparison.Ordinal))
                {
                    return normalized.Substring(0, normalized.Length - suffix.Length);
                }
            }

            return normalized;
        }
    }
}
