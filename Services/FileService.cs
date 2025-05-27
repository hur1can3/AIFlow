namespace AIFlow.Cli.Services
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    public static class FileService
    {
        public static string? CalculateFileHash(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("ErrorCalculatingHash", filePath, ex.Message)
                );
                return null;
            }
        }

        public static string Base64Encode(string plainText) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

        public static string Base64Decode(string base64EncodedData)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedData));
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("ErrorDecodingBase64", ex.Message)
                );
                return string.Empty;
            }
        }

        public static string GetProjectRelativePath(string fullPath)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (
                !currentDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())
                && !currentDirectory.EndsWith(Path.AltDirectorySeparatorChar.ToString())
            )
            {
                currentDirectory += Path.DirectorySeparatorChar;
            }
            Uri projectUri = new Uri(currentDirectory);
            Uri fileUri = new Uri(Path.GetFullPath(fullPath));

            if (projectUri.IsBaseOf(fileUri))
            {
                return Uri.UnescapeDataString(
                    projectUri
                        .MakeRelativeUri(fileUri)
                        .ToString()
                        .Replace(Path.DirectorySeparatorChar, '/')
                );
            }
            return fullPath.Replace(Path.DirectorySeparatorChar, '/');
        }

        public static string GetFullPath(string projectRelativePath)
        {
            return Path.GetFullPath(projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
