using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace aafccore.util
{
    internal static class Hasher
    {
        private static StringBuilder hashBuilder = new StringBuilder(258);
        internal static string CreateHash(string dataToHash)
        {
            hashBuilder.Clear();
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));

                
                for (int i = 0; i < bytes.Length; i++)
                {
                    hashBuilder.Append(bytes[i].ToString("x2"));
                }
                return hashBuilder.ToString();
            }
        }
    }
}
