using System;
using System.Text;
using System.Security.Cryptography;

namespace StayTravel.Models
{
    public static class PasswordHelper
    {
        // Hàm mã hóa chuỗi mật khẩu sang chuỗi SHA-256 Hash
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}