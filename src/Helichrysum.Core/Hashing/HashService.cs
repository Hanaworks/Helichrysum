namespace Helichrysum.Core.Hashing;

using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;

/// <summary>
/// Provides layered hash computation: CRC32 for fast pre-screening,
/// SHA256 for strong confirmation. Follows the tiered upgrade strategy.
/// </summary>
public static class HashService
{
    /// <summary>
    /// Computes the CRC32 checksum of a file (fast, for pre-screening).
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <returns>The CRC32 value as a uint.</returns>
    public static uint ComputeCrc32(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        return Crc32.HashToUInt32(bytes);
    }

    /// <summary>
    /// Computes the SHA256 hash of a file (strong, for confirmation).
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <returns>The SHA256 hash as a lowercase hex string.</returns>
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }
}