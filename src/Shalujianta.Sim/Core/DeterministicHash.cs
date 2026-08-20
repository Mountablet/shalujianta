namespace Shalujianta.Sim.Core;

/// <summary>
/// STS2 uses StringHelper.GetDeterministicHashCode on snake_case RNG names.
/// Algorithm matches community reproductions (tck.mn / sts2_rng_predictor).
/// </summary>
public static class DeterministicHash
{
    public static int GetDeterministicHashCode(string text)
    {
        int num = 352654597;
        int num2 = num;
        for (int i = 0; i < text.Length; i += 2)
        {
            num = ((num << 5) + num) ^ text[i];
            if (i == text.Length - 1)
            {
                break;
            }

            num2 = ((num2 << 5) + num2) ^ text[i + 1];
        }

        return num + (num2 * 1566083941);
    }

    public static string SnakeCase(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            return text;
        }

        var chars = new List<char>(text.Length + 8);
        chars.Add(char.ToLowerInvariant(text[0]));
        for (int i = 1; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsUpper(c) && char.IsLetterOrDigit(text[i - 1]))
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(c));
        }

        return new string(chars.ToArray());
    }
}
