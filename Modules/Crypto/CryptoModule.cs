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
        public int Priority => 20; // 优先级适中，可以在文件系统之后初始化

        // AES 加密需要的密钥 (长度必须是 16, 24, 或 32 个字符)
        private byte[] _key;
        // AES 加密需要的初始化向量 (长度必须是 16 个字符)
        private byte[] _iv;
        
        private bool _isInitialized = false;

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

            // 验证长度
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            {
                Log.Error("[Crypto] 密钥(Key)长度必须是 16, 24 或 32 个字符！");
                return;
            }

            if (iv.Length != 16)
            {
                Log.Error("[Crypto] 初始化向量(IV)长度必须是 16 个字符！");
                return;
            }

            _key = Encoding.UTF8.GetBytes(key);
            _iv = Encoding.UTF8.GetBytes(iv);
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
    }
}