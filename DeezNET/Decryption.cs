using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using System.Security.Cryptography;
using System.Text;

namespace DeezNET;

internal static class Decryption
{
    private const int BLOCK_SIZE = 2048 * 3;

    public static void DecodeTrackStream(Stream input, Stream output, bool isCrypted, string blowfishKey)
    {
        bool isStart = true;

        int bytesRead;
        byte[] buffer = new byte[BLOCK_SIZE];
        while ((bytesRead = ReadBlock(input, buffer)) > 0)
        {
            int offset = DecodeBlock(buffer, bytesRead, isCrypted, blowfishKey, isStart);
            isStart = false;

            // Only the bytes read this pass; the rest of the buffer still holds the previous block.
            output.Write(buffer, offset, bytesRead - offset);
        }
    }

    /// <summary>
    /// Decodes a track stream without blocking, so a slow or stalled download can be cancelled.
    /// </summary>
    /// <param name="readTimeout">The longest to wait for more data before giving up with a <see cref="TimeoutException"/>. Null waits indefinitely.</param>
    public static async Task DecodeTrackStreamAsync(Stream input, Stream output, bool isCrypted, string blowfishKey, TimeSpan? readTimeout = null, CancellationToken token = default)
    {
        bool isStart = true;

        int bytesRead;
        byte[] buffer = new byte[BLOCK_SIZE];
        while ((bytesRead = await ReadBlockAsync(input, buffer, readTimeout, token)) > 0)
        {
            int offset = DecodeBlock(buffer, bytesRead, isCrypted, blowfishKey, isStart);
            isStart = false;

            await output.WriteAsync(buffer.AsMemory(offset, bytesRead - offset), token);
        }
    }

    // Decrypts the block in place and returns where its audio starts.
    private static int DecodeBlock(byte[] buffer, int bytesRead, bool isCrypted, string blowfishKey, bool isStart)
    {
        if (isCrypted && bytesRead >= 2048)
        {
            DecryptChunk(blowfishKey, buffer.AsSpan(0, 2048)).CopyTo(buffer, 0);
        }

        int offset = 0;
        if (isStart && bytesRead >= 8 && buffer[0] == 0 && !Encoding.UTF8.GetString(buffer, 4, 4).Equals("ftyp"))
        {
            while (offset < bytesRead && buffer[offset] == 0)
                offset++;

            if (offset == bytesRead)
                offset = 0;
        }

        return offset;
    }

    // Fills the buffer unless the stream ends, so every block keeps the 6144-byte stripe alignment.
    private static int ReadBlock(Stream input, byte[] buffer)
    {
        int total = 0;
        int read;
        while (total < buffer.Length && (read = input.Read(buffer, total, buffer.Length - total)) > 0)
            total += read;

        return total;
    }

    private static async Task<int> ReadBlockAsync(Stream input, byte[] buffer, TimeSpan? readTimeout, CancellationToken token)
    {
        using CancellationTokenSource readCts = CancellationTokenSource.CreateLinkedTokenSource(token);

        int total = 0;
        while (total < buffer.Length)
        {
            // restarted for every read, so it limits how long the connection can stall, not how long the whole track takes
            if (readTimeout != null)
                readCts.CancelAfter(readTimeout.Value);

            int read;
            try
            {
                read = await input.ReadAsync(buffer.AsMemory(total, buffer.Length - total), readCts.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // a timeout rather than a cancellation, so callers that ignore cancellations still see a failure
                throw new TimeoutException($"No data was received for {readTimeout!.Value.TotalSeconds} seconds.");
            }

            if (read == 0)
                break;

            total += read;
        }

        return total;
    }

    public static string GenerateBlowfishKey(string trackId)
    {
        const string SECRET = "g4el58wc0zvf9na1";
        string md5Id = CalculateMD5(trackId);
        string bfKey = "";
        for (int i = 0; i < 16; i++)
            bfKey += (char)(md5Id[i] ^ md5Id[i + 16] ^ SECRET[i]);

        return bfKey;
    }

    public static string CalculateMD5(string data)
    {
        byte[] hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(data));
        StringBuilder sb = new();

        foreach (byte b in hashBytes)
            sb.Append(b.ToString("x2"));

        return sb.ToString();
    }

    public static byte[] DecryptChunk(string key, ReadOnlySpan<byte> data)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        var engine = new BlowfishEngine();
        var cipher = new BufferedBlockCipher(new CbcBlockCipher(engine));
        var keyParam = ParameterUtilities.CreateKeyParameter("Blowfish", keyBytes);
        var parameters = new ParametersWithIV(keyParam, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
        cipher.Init(false, parameters);

        var decryptedBytes = new byte[cipher.GetOutputSize(data.Length)];
        var len = cipher.ProcessBytes(data.ToArray(), 0, data.Length, decryptedBytes, 0);
        len += cipher.DoFinal(decryptedBytes, len);

        Array.Resize(ref decryptedBytes, len);

        return decryptedBytes;
    }
}
