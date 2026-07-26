using System;

namespace GameSaveCenter.Contracts
{
    /// <summary>
    /// Central protocol constants shared by the Playnite plugin and the Worker.
    /// Changing these values is a compatibility change and must be coordinated.
    /// </summary>
    public static class ProtocolConstants
    {
        /// <summary>
        /// Named pipe used for local, current-user IPC.
        /// </summary>
        public const string PipeName = "GameSaveCenter.Worker.v1";

        /// <summary>
        /// Current JSON envelope protocol version.
        /// </summary>
        public const int ProtocolVersion = 1;

        /// <summary>
        /// Maximum accepted JSON message size. This protects the pipe from accidental
        /// unbounded payloads; large file lists are paginated by the Worker.
        /// </summary>
        public const int MaximumMessageBytes = 4 * 1024 * 1024;

        /// <summary>
        /// Default timeout used by short request/response commands.
        /// </summary>
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);
    }
}
