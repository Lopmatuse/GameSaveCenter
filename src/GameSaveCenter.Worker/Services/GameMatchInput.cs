using System.Security.Cryptography;
using System.Text;
using GameSaveCenter.Contracts;

namespace GameSaveCenter.Worker.Services;

/// <summary>
/// Produces the stable subset of a Playnite descriptor that can affect Ludusavi matching.
/// UI-only metadata and install state deliberately do not invalidate a valid match.
/// </summary>
public static class GameMatchInput
{
    public static string CreateHash(GameDescriptorDto game)
    {
        var value = string.Join(
            "\n",
            (game.Name ?? string.Empty).Trim().ToUpperInvariant(),
            ((int)game.Platform).ToString(),
            (game.PlatformGameId ?? string.Empty).Trim().ToUpperInvariant());
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}
