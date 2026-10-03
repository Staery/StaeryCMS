using System.Text;
using System.Text.RegularExpressions;

namespace StaeryCMS.Core.Services;

/// <summary>Builds URL-friendly slugs from titles, with Cyrillic transliteration.</summary>
public static partial class SlugGenerator
{
    public const int MaxLength = 80;
    public const string Fallback = "untitled";

    private const char Breve = '\u0306';

    // 'й', 'ё' and 'ї' are absent on purpose: FormD decomposes them into a base letter plus a combining mark.
    private static readonly Dictionary<char, string> Transliteration = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e",
        ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['к'] = "k", ['л'] = "l", ['м'] = "m",
        ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch",
        ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
        ['і'] = "i", ['є'] = "ye", ['ґ'] = "g",
    };

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex ValidSlugRegex();

    public static bool IsValid(string? slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= MaxLength && ValidSlugRegex().IsMatch(slug);

    public static string Generate(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Fallback;
        }

        var builder = new StringBuilder(title.Length);
        var pendingSeparator = false;

        foreach (var ch in title.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            string? chunk = null;

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                chunk = ch.ToString();
            }
            else if (Transliteration.TryGetValue(ch, out var latin))
            {
                chunk = latin;
            }
            else if (char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                // Strip accents ("café" -> "cafe"), except the breve that turns 'и' into 'й'.
                if (ch == Breve && builder.Length > 0 && builder[^1] == 'i')
                {
                    builder[^1] = 'y';
                }

                continue;
            }

            if (chunk is null)
            {
                pendingSeparator = builder.Length > 0;
                continue;
            }

            if (chunk.Length == 0)
            {
                continue;
            }

            if (pendingSeparator)
            {
                builder.Append('-');
                pendingSeparator = false;
            }

            builder.Append(chunk);
        }

        var slug = builder.ToString();
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        return slug.Length == 0 ? Fallback : slug;
    }

    /// <summary>Appends -2, -3, ... until the slug is not in <paramref name="taken"/>.</summary>
    public static string MakeUnique(string slug, ISet<string> taken)
    {
        if (!taken.Contains(slug))
        {
            return slug;
        }

        for (var i = 2; ; i++)
        {
            var suffix = "-" + i;
            var baseSlug = slug.Length + suffix.Length > MaxLength ? slug[..(MaxLength - suffix.Length)].TrimEnd('-') : slug;
            var candidate = baseSlug + suffix;
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
