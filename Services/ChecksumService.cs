using System;
using System.IO.Hashing; // Requires .NET 6+
using System.Text;

namespace AIFlow.Cli.Services
{
    /// <summary>
    /// Provides checksum calculation services.
    /// </summary>
    public static class ChecksumService
    {
        /// <summary>
        /// Calculates the CRC32 checksum for the given byte array.
        /// </summary>
        /// <param name="data">The byte array to calculate the checksum for.</param>
        /// <returns>A hexadecimal string representation of the CRC32 checksum.</returns>
        public static string CalculateCRC32(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            var crc32 = new Crc32();
            crc32.Append(data);
            byte[] hashBytes = crc32.GetCurrentHash();
            // Convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
