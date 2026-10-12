using System;

namespace RoguePirates.Hackathon
{
    [Serializable] public sealed class WalletNonceRequest { public string wallet; }
    [Serializable] public sealed class WalletNonceResponse { public string wallet; public string nonce; public string message; public string expiresAt; }
    [Serializable] public sealed class WalletVerifyRequest { public string wallet; public string signature; public string message; public string name; }
    [Serializable]
    public sealed class WalletVerifyResponse
    {
        public string playerId; public string token; public string name; public string tag;
        public string wallet; public string email; public bool isNew;
    }
    [Serializable] public sealed class EmailRequest { public string email; }
    [Serializable] public sealed class ProfileNameRequest { public string name; }
    [Serializable] public sealed class ProfileNameResponse { public string playerId; public string name; public string tag; }
    [Serializable] public sealed class EmailResponse { public string playerId; public string wallet; public string email; public bool emailVerified; }
}
