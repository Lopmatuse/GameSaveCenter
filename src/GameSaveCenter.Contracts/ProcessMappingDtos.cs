namespace GameSaveCenter.Contracts;

/// <summary>User-confirmed association between an executable name and one Playnite game.</summary>
public sealed class ProcessMappingDto
{
    public string ExecutableName { get; set; } = string.Empty;
    public string PlayniteId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
