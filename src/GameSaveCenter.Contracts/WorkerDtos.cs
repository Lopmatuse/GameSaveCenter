using System;

namespace GameSaveCenter.Contracts
{
    /// <summary>
    /// Lightweight Worker handshake returned by the stable named pipe.
    /// The plugin uses this to reject a healthy-but-stale Worker left behind by
    /// an older installed extension version.
    /// </summary>
    public sealed class WorkerPingDto
    {
        public DateTime Utc { get; set; }
        public string Version { get; set; } = string.Empty;
    }
}
