using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 表数据 / 存档封装：AES-256-CBC + HMAC-SHA256。
/// 格式：PAT1 | ver | iv(16) | mac(32) | ciphertext
/// </summary>
public static class SecureCodec
{
    public const byte Version = 1;
    static readonly byte[] Magic = { (byte)'P', (byte)'A', (byte)'T', (byte)'1' };
    const int IvSize = 16;
    const int MacSize = 32;

    public static byte[] Encrypt(byte[] plain)
    {
        if (plain == null) plain = Array.Empty<byte>();
        byte[] iv = new byte[IvSize];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(iv);

        byte[] cipher;
        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = GameSecrets.AesKey();
            aes.IV = iv;
            using (var enc = aes.CreateEncryptor())
                cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
        }

        byte[] mac = ComputeMac(Version, iv, cipher);
        var output = new byte[Magic.Length + 1 + IvSize + MacSize + cipher.Length];
        int o = 0;
        Buffer.BlockCopy(Magic, 0, output, o, Magic.Length);
        o += Magic.Length;
        output[o++] = Version;
        Buffer.BlockCopy(iv, 0, output, o, IvSize);
        o += IvSize;
        Buffer.BlockCopy(mac, 0, output, o, MacSize);
        o += MacSize;
        Buffer.BlockCopy(cipher, 0, output, o, cipher.Length);
        return output;
    }

    public static byte[] EncryptUtf8(string text)
    {
        return Encrypt(Encoding.UTF8.GetBytes(text ?? ""));
    }

    public static bool TryDecrypt(byte[] blob, out byte[] plain)
    {
        plain = null;
        if (blob == null || blob.Length < Magic.Length + 1 + IvSize + MacSize + 16)
            return false;
        for (int i = 0; i < Magic.Length; i++)
            if (blob[i] != Magic[i])
                return false;

        int o = Magic.Length;
        byte ver = blob[o++];
        if (ver != Version) return false;

        var iv = new byte[IvSize];
        Buffer.BlockCopy(blob, o, iv, 0, IvSize);
        o += IvSize;
        var mac = new byte[MacSize];
        Buffer.BlockCopy(blob, o, mac, 0, MacSize);
        o += MacSize;
        int cipherLen = blob.Length - o;
        if (cipherLen < 16) return false;
        var cipher = new byte[cipherLen];
        Buffer.BlockCopy(blob, o, cipher, 0, cipherLen);

        byte[] expect = ComputeMac(ver, iv, cipher);
        if (!FixedEquals(mac, expect))
            return false;

        try
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = GameSecrets.AesKey();
                aes.IV = iv;
                using (var dec = aes.CreateDecryptor())
                    plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            }
            return plain != null;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static bool TryDecryptUtf8(byte[] blob, out string text)
    {
        text = null;
        if (!TryDecrypt(blob, out byte[] plain) || plain == null)
            return false;
        text = Encoding.UTF8.GetString(plain);
        return true;
    }

    /// <summary>存档兼容：加密包，或旧版明文 JSON。</summary>
    public static bool TryReadPayload(byte[] blob, out string json)
    {
        json = null;
        if (blob == null || blob.Length == 0) return false;
        if (TryDecryptUtf8(blob, out json) && LooksLikeJson(json))
            return true;
        try
        {
            json = Encoding.UTF8.GetString(blob);
            if (json.Length > 0 && json[0] == '\uFEFF')
                json = json.Substring(1);
            return LooksLikeJson(json);
        }
        catch
        {
            return false;
        }
    }

    static bool LooksLikeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i < s.Length && (s[i] == '{' || s[i] == '[');
    }

    static byte[] ComputeMac(byte ver, byte[] iv, byte[] cipher)
    {
        using (var hmac = new HMACSHA256(GameSecrets.MacKey()))
        using (var ms = new MemoryStream())
        {
            ms.WriteByte(ver);
            ms.Write(iv, 0, iv.Length);
            ms.Write(cipher, 0, cipher.Length);
            return hmac.ComputeHash(ms.ToArray());
        }
    }

    static bool FixedEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        int d = 0;
        for (int i = 0; i < a.Length; i++)
            d |= a[i] ^ b[i];
        return d == 0;
    }
}
