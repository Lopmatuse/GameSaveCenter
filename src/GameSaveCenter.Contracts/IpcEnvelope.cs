using System;

namespace GameSaveCenter.Contracts
{
    /// <summary>
    /// Transport envelope for newline-delimited JSON messages over the named pipe.
    /// PayloadJson intentionally stays opaque at this layer to keep the protocol
    /// compatible between .NET Framework and modern .NET serializers.
    /// </summary>
    public sealed class IpcEnvelope
    {
        public int ProtocolVersion { get; set; } = ProtocolConstants.ProtocolVersion;
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
        public string Type { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public bool IsResponse { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = "{}";
    }
}
