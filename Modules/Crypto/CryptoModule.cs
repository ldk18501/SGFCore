using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局加密解密模块 (基于 AES 对称加密)
    /// </summary>
    public class CryptoModule : IFrameworkModule
    {
        private const string AuthenticatedStringPrefix = "SGF2:";
        // AES 加密需要的密钥 (长度必须是 16, 24, 或 32 个字符)
        private byte[] _key;
        // AES 加密需要的初始化向量 (长度必须是 16 个字符)
        private byte[] _iv;
        
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;

        public void OnInit()
        {
            // 默认不设置 Key，等待游戏业务层主动调用 SetCryptoKey 进行配置
            Log.Module("Crypto", "加密模块已加载，等待业务层注入密钥...");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }

        public void OnDestroy()
        {
            // 清理内存中的密钥数据，增加安全性
            if (_key != null) Array.Clear(_key, 0, _key.Length);
            if (_iv != null) Array.Clear(_iv, 0, _iv.Length);
            _isInitialized = false;
        }

        /// <summary>
        /// 设置加密密钥 (强烈建议在游戏启动的最早期调用)
        /// </summary>
        /// <param name="key">16, 24 或 32位长度的字符串</param>
        /// <param name="iv">16位长度的字符串</param>
        public void SetCryptoKey(string key, string iv)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
            {
                Log.Error("[Crypto] Key 或 IV 不能为空！");
                return;
            }

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] ivBytes = Encoding.UTF8.GetBytes(iv);

            if (keyBytes.Length != 16 && keyBytes.Length != 24 && keyBytes.Length != 32)
            {
                Log.Error("[Crypto] 密钥(Key)的 UTF-8 字节长度必须是 16、24 或 32！");
                return;
            }

            if (ivBytes.Length != 16)
            {
                Log.Error("[Crypto] 兼容初始化向量(IV)的 UTF-8 字节长度必须是 16！");
                return;
            }

            if (_key != null) Array.Clear(_key, 0, _key.Length);
            if (_iv != null) Array.Clear(_iv, 0, _iv.Length);
            _key = keyBytes;
            _iv = ivBytes;
            _isInitialized = true;
            
            Log.Module("Crypto", "密钥注入成功，加密模块准备就绪。");
        }

        // ==========================================
        // API: 字符串加解密 (常用于 JSON 存档)
        // ==========================================

        public string EncryptString(string plainText)
        {
            if (!_isInitialized) throw new Exception("CryptoModule 未初始化密钥！");
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] encryptedBytes = EncryptBytes(Encoding.UTF8.GetBytes(plainText));
            // 将加密后的 byte 数组转为 Base64 字符串，方便作为文本保存
            return Convert.ToBase64String(encryptedBytes);
        }

        public string DecryptString(string encryptedBase64Text)
        {
            if (!_isInitialized) throw new Exception("CryptoModule 未初始化密钥！");
            if (string.IsNullOrEmpty(encryptedBase64Text)) return encryptedBase64Text;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64Text);
                byte[] decryptedBytes = DecryptBytes(encryptedBytes);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception e)
            {
                Log.Error($"[Crypto] 解密失败，可能密钥错误或存档被篡改: {e.Message}");
                return null; // 解密失败通常意味着存档损坏或被改动
            }
        }

        /// <summary>
        /// 新存档格式：随机 IV + HMAC-SHA256，前缀用于和旧 AES-CBC Base64 格式区分。
        /// </summary>
        public string EncryptAuthenticatedString(string plainText)
        {
            if (!_isInitialized) throw new Exception("CryptoModule 未初始化密钥！");
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (Aes aes = Aes.Create())
            {
                aes.Key = _key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                byte[] cipherBytes = EncryptBytesWithIv(
                    Encoding.UTF8.GetBytes(plainText),
                    aes.IV);
                byte[] authenticatedBytes = new byte[aes.IV.Length + cipherBytes.Length];
                Buffer.BlockCopy(aes.IV, 0, authenticatedBytes, 0, aes.IV.Length);
                Buffer.BlockCopy(cipherBytes, 0, authenticatedBytes, aes.IV.Length, cipherBytes.Length);

                byte[] tag;
                byte[] macKey = CreateMacKey();
                using (var hmac = new HMACSHA256(macKey))
                {
                    tag = hmac.ComputeHash(authenticatedBytes);
                }
                Array.Clear(macKey, 0, macKey.Length);

                byte[] envelope = new byte[authenticatedBytes.Length + tag.Length];
                Buffer.BlockCopy(authenticatedBytes, 0, envelope, 0, authenticatedBytes.Length);
                Buffer.BlockCopy(tag, 0, envelope, authenticatedBytes.Length, tag.Length);
                return AuthenticatedStringPrefix + Convert.ToBase64String(envelope);
            }
        }

        /// <summary>
        /// 自动识别 SGF2 新格式；没有前缀时回退到旧固定 IV 格式，保证旧存档可读。
        /// </summary>
        public string DecryptAuthenticatedString(string encryptedText)
        {
            if (!_isInitialized) throw new Exception("CryptoModule 未初始化密钥！");
            if (string.IsNullOrEmpty(encryptedText)) return encryptedText;
            if (!encryptedText.StartsWith(AuthenticatedStringPrefix, StringComparison.Ordinal))
            {
                return DecryptString(encryptedText);
            }

            try
            {
                byte[] envelope = Convert.FromBase64String(
                    encryptedText.Substring(AuthenticatedStringPrefix.Length));
                const int ivLength = 16;
                const int tagLength = 32;
                if (envelope.Length <= ivLength + tagLength)
                {
                    throw new CryptographicException("加密存档数据长度无效。");
                }

                int authenticatedLength = envelope.Length - tagLength;
                byte[] expectedTag;
                byte[] macKey = CreateMacKey();
                using (var hmac = new HMACSHA256(macKey))
                {
                    expectedTag = hmac.ComputeHash(envelope, 0, authenticatedLength);
                }
                Array.Clear(macKey, 0, macKey.Length);

                if (!FixedTimeEquals(envelope, authenticatedLength, expectedTag))
                {
                    throw new CryptographicException("存档完整性校验失败。");
                }

                byte[] iv = new byte[ivLength];
                Buffer.BlockCopy(envelope, 0, iv, 0, ivLength);
                int cipherLength = authenticatedLength - ivLength;
                byte[] cipherBytes = new byte[cipherLength];
                Buffer.BlockCopy(envelope, ivLength, cipherBytes, 0, cipherLength);
                return Encoding.UTF8.GetString(DecryptBytesWithIv(cipherBytes, iv));
            }
            catch (Exception exception)
            {
                Log.Error($"[Crypto] 认证解密失败: {exception.Message}");
                return null;
            }
        }

        // ==========================================
        // API: 字节流加解密 (常用于二进制文件、AssetBundle保护)
        // ==========================================

        public byte[] EncryptBytes(byte[] plainBytes)
        {
            if (!_isInitialized) throw new Exception("CryptoModule 未初始化密钥！");
            if (plainBytes == null || plainBytes.Length == 0) return plainBytes;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _key;
                aesAlg.IV = _iv;
                aesAlg.Mode = CipherMode.CBC; // 密码块链模式，安全性高
                aesAlg.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(plainBytes, 0, plainBytes.Length);
                        csEncrypt.FlushFinalBlock();
                        return msEncrypt.ToArray();
                    }
                }
            }
        }

        public byte[] DecryptBytes(byte[] encryptedBytes)
        {
            if (!_isInitialized) throw new Exception("CryptoModule 未初始化密钥！");
            if (encryptedBytes == null || encryptedBytes.Length == 0) return encryptedBytes;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _key;
                aesAlg.IV = _iv;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(encryptedBytes))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (MemoryStream msResult = new MemoryStream())
                        {
                            csDecrypt.CopyTo(msResult);
                            return msResult.ToArray();
                        }
                    }
                }
            }
        }

        private byte[] EncryptBytesWithIv(byte[] plainBytes, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (var output = new MemoryStream())
                using (var cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    cryptoStream.FlushFinalBlock();
                    return output.ToArray();
                }
            }
        }

        private byte[] DecryptBytesWithIv(byte[] encryptedBytes, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                using (var input = new MemoryStream(encryptedBytes))
                using (var cryptoStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
                using (var output = new MemoryStream())
                {
                    cryptoStream.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        private static bool FixedTimeEquals(byte[] envelope, int tagOffset, byte[] expectedTag)
        {
            if (expectedTag == null || envelope.Length - tagOffset != expectedTag.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < expectedTag.Length; i++)
            {
                difference |= envelope[tagOffset + i] ^ expectedTag[i];
            }

            return difference == 0;
        }

        private byte[] CreateMacKey()
        {
            byte[] label = Encoding.UTF8.GetBytes("SGFCore.Save.HMAC.v2");
            byte[] material = new byte[_key.Length + label.Length];
            Buffer.BlockCopy(_key, 0, material, 0, _key.Length);
            Buffer.BlockCopy(label, 0, material, _key.Length, label.Length);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] result = sha256.ComputeHash(material);
                Array.Clear(material, 0, material.Length);
                return result;
            }
        }
    }
}
