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
    public static void DecodeTrackStream(Stream input, Stream output, bool isCrypted, string blowfishKey)
    {
        bool isStart = true;

        int bytesRead;
        byte[] buffer = new byte[2048 * 3];
        while ((bytesRead = ReadBlock(input, buffer)) > 0)
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

            isStart = false;

            // Only the bytes read this pass; the rest of the buffer still holds the previous block.
            output.Write(buffer, offset, bytesRead - offset);
        }
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
