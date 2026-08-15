namespace Helichrysum.Core.Hashing;

using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;

/// <summary>
/// Provides layered hash computation: CRC32 for fast pre-screening,
/// sampled hash for intermediate tier, SHA256 for strong confirmation.
/// </summary>
public static class HashService
{
    private const int SampledHeadSize = 16 * 1024;     // 16 KB
    private const int SampledMiddleSize = 32 * 1024;   // 32 KB
    private const int SampledTailSize = 16 * 1024;     // 16 KB
    private const int SampledFullThreshold = 64 * 1024; // 64 KB

    /// <summary>
    /// Computes a sampled hash by reading head, middle, and tail sections of a file.
    /// For files smaller than 64 KB, the entire file is read.
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <returns>A sampled hash result with the hash value and bytes read.</returns>
    public static SampledHashResult ComputeSampled(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        long fileLength = stream.Length;

        if (fileLength <= SampledFullThreshold)
        {
            // Small file: read everything.
            byte[] allBytes = new byte[fileLength];
            stream.ReadExactly(allBytes);
            uint hash = XxHash32.HashToUInt32(allBytes);
            return new SampledHashResult
            {
                HashValue = hash.ToString("x8"),
                BytesRead = fileLength,
            };
        }

        // Large file: read head, middle, and tail sections.
        var hasher = new XxHash32();

        // Read head.
        byte[] headBuffer = new byte[SampledHeadSize];
        stream.ReadExactly(headBuffer);
        hasher.Append(headBuffer);

        // Read middle (skip to the middle of the file).
        long middleOffset = (fileLength - SampledMiddleSize) / 2;
        stream.Seek(middleOffset, SeekOrigin.Begin);
        byte[] middleBuffer = new byte[SampledMiddleSize];
        stream.ReadExactly(middleBuffer);
        hasher.Append(middleBuffer);

        // Read tail.
        stream.Seek(fileLength - SampledTailSize, SeekOrigin.Begin);
        byte[] tailBuffer = new byte[SampledTailSize];
        stream.ReadExactly(tailBuffer);
        hasher.Append(tailBuffer);

        uint finalHash = hasher.GetCurrentHashAsUInt32();
        long totalBytesRead = SampledHeadSize + SampledMiddleSize + SampledTailSize;

        return new SampledHashResult
        {
            HashValue = finalHash.ToString("x8"),
            BytesRead = totalBytesRead,
        };
    }

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