using System.Text;
using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace RoguePirates.Api.Solana;

/// <summary>
/// The few Metaplex Token Metadata and Associated Token Account instructions the API needs, built by hand (there is no
/// Solnet.Metaplex package on NuGet for .NET 10). Layouts follow mpl-token-metadata (CreateMetadataAccountV3 = 33,
/// CreateMasterEditionV3 = 17; Borsh encoding).
/// </summary>
public static class Metaplex
{
    public static readonly PublicKey TokenMetadataProgram = new("metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s");
    public static readonly PublicKey SysvarRent = new("SysvarRent111111111111111111111111111111111");

    public static PublicKey MetadataPda(PublicKey mint)
    {
        PublicKey.TryFindProgramAddress(new[] { Encoding.UTF8.GetBytes("metadata"), TokenMetadataProgram.KeyBytes, mint.KeyBytes },
            TokenMetadataProgram, out var pda, out _);
        return pda;
    }

    public static PublicKey MasterEditionPda(PublicKey mint)
    {
        PublicKey.TryFindProgramAddress(new[]
        {
            Encoding.UTF8.GetBytes("metadata"), TokenMetadataProgram.KeyBytes, mint.KeyBytes, Encoding.UTF8.GetBytes("edition"),
        }, TokenMetadataProgram, out var pda, out _);
        return pda;
    }

    static void BorshString(List<byte> data, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        data.AddRange(BitConverter.GetBytes((uint)bytes.Length));
        data.AddRange(bytes);
    }

    /// <summary>CreateMetadataAccountV3: name (max 32 bytes), symbol (max 10), uri (max 200); no creators, mutable.</summary>
    public static TransactionInstruction CreateMetadataAccountV3(PublicKey mint, PublicKey mintAuthority, PublicKey payer,
        PublicKey updateAuthority, string name, string symbol, string uri)
    {
        var data = new List<byte> { 33 };
        BorshString(data, name);
        BorshString(data, symbol);
        BorshString(data, uri);
        data.AddRange(BitConverter.GetBytes((ushort)0)); // seller_fee_basis_points
        data.Add(0); // creators: None
        data.Add(0); // collection: None
        data.Add(0); // uses: None
        data.Add(1); // is_mutable
        data.Add(0); // collection_details: None
        return new TransactionInstruction
        {
            ProgramId = TokenMetadataProgram.KeyBytes,
            Keys = new List<AccountMeta>
            {
                AccountMeta.Writable(MetadataPda(mint), false),
                AccountMeta.ReadOnly(mint, false),
                AccountMeta.ReadOnly(mintAuthority, true),
                AccountMeta.Writable(payer, true),
                AccountMeta.ReadOnly(updateAuthority, true),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(SysvarRent, false),
            },
            Data = data.ToArray(),
        };
    }

    /// <summary>CreateMasterEditionV3 with max supply 0: a one-of-one NFT (the mint authority moves to the edition).</summary>
    public static TransactionInstruction CreateMasterEditionV3(PublicKey mint, PublicKey updateAuthority, PublicKey mintAuthority, PublicKey payer)
    {
        var data = new List<byte> { 17, 1 }; // Some(max_supply)
        data.AddRange(BitConverter.GetBytes((ulong)0));
        return new TransactionInstruction
        {
            ProgramId = TokenMetadataProgram.KeyBytes,
            Keys = new List<AccountMeta>
            {
                AccountMeta.Writable(MasterEditionPda(mint), false),
                AccountMeta.Writable(mint, false),
                AccountMeta.ReadOnly(updateAuthority, true),
                AccountMeta.ReadOnly(mintAuthority, true),
                AccountMeta.Writable(payer, true),
                AccountMeta.Writable(MetadataPda(mint), false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(SysvarRent, false),
            },
            Data = data.ToArray(),
        };
    }

    /// <summary>Associated Token Account "CreateIdempotent" (does nothing if the account exists).</summary>
    public static TransactionInstruction CreateAtaIdempotent(PublicKey payer, PublicKey owner, PublicKey mint) => new()
    {
        ProgramId = AssociatedTokenAccountProgram.ProgramIdKey.KeyBytes,
        Keys = new List<AccountMeta>
        {
            AccountMeta.Writable(payer, true),
            AccountMeta.Writable(AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, mint), false),
            AccountMeta.ReadOnly(owner, false),
            AccountMeta.ReadOnly(mint, false),
            AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
        },
        Data = new byte[] { 1 },
    };
}
