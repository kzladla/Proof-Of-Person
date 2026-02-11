namespace Unity.Relay.Editor
{
    /// <summary>
    /// Protocol constants for relay version negotiation and capability checking.
    /// </summary>
    static class RelayProtocol
    {
        /// <summary>
        /// Minimum protocol version required by this editor.
        /// Relays with older protocol versions will be treated as incompatible.
        /// </summary>
        public const string MinimumProtocolVersion = "1.0";

        /// <summary>
        /// Known relay capabilities that can be negotiated during handshake.
        /// </summary>
        public static class Capabilities
        {
            public const string Acp = "acp";
            public const string Replay = "replay";
        }
    }
}
