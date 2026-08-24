using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 派生加解密密钥。不在磁盘明文存密码；拆字节后运行时混合。
/// 客户端密钥可被逆向，用于防普通改档/改表，不能当服务端权威。
/// </summary>
public static class GameSecrets
{
    static readonly byte[] PartA =
    {
        0x3A, 0x91, 0xC2, 0x07, 0x5E, 0xB8, 0x44, 0x6D,
        0x19, 0xF0, 0xA3, 0x2C, 0x77, 0x8B, 0xE1, 0x54
    };

    static readonly byte[] PartB =
    {
        0x62, 0x0D, 0x9F, 0xC8, 0x31, 0x4A, 0xD5, 0x16,
        0xBE, 0x73, 0x08, 0xE9, 0xA6, 0x2F, 0x50, 0xCB
    };

    const string MixSalt = "PxAdv.RiftBlade.v1";

    public static byte[] AesKey()
    {
        return Derive("AES/v1");
    }

    public static byte[] MacKey()
    {
        return Derive("MAC/v1");
    }

    static byte[] Derive(string tag)
    {
        byte[] mat = BuildMaterial();
        byte[] suffix = Encoding.UTF8.GetBytes(tag);
        byte[] buf = new byte[mat.Length + suffix.Length];
        Buffer.BlockCopy(mat, 0, buf, 0, mat.Length);
        Buffer.BlockCopy(suffix, 0, buf, mat.Length, suffix.Length);
        using (var sha = SHA256.Create())
            return sha.ComputeHash(buf);
    }

    static byte[] BuildMaterial()
    {
        byte[] salt = Encoding.UTF8.GetBytes(MixSalt);
        var m = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            byte a = PartA[i % PartA.Length];
            byte b = PartB[i % PartB.Length];
            byte s = salt[i % salt.Length];
            m[i] = (byte)(a ^ b ^ s ^ (i * 13 + 7));
        }
        return m;
    }
}
