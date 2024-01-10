using System;
using System.Collections.Generic;
using System.Text;

namespace CSVFileReceiver
{
    public enum FileType
    {
        Standard,
        AAA,
        ProductDevelopment
    }

    public static class FileTypeExtensions
    {
        public static string ToString(this FileType self)
        {
            switch (self)
            {
                case FileType.Standard:
                    return "standard";
                case FileType.AAA:
                    return "aaa";
                case FileType.ProductDevelopment:
                    return "pd";
                default:
                    throw new ArgumentException($"Unexpected value {self}");
            }
        }

        public static FileType ToFileType(this string self)
        {
            switch (self.ToLower())
            {
                case "standard":
                    return FileType.Standard;
                case "aaa":
                    return FileType.AAA;
                case "pd":
                    return FileType.ProductDevelopment;
                default:
                    throw new ArgumentException($"Unexpected value {self}");
            }
        }

        public static bool IsExists(this FileType self)
        {
            return Enum.IsDefined(typeof(FileType), self);
        }
    }
}
