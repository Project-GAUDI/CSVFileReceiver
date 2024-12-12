using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TICO.GAUDI.Commons;

namespace IotedgeV2CSVFileReceiver
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
            string result = null;
            switch (self)
            {
                case FileType.Standard:
                    result = "standard";
                    break;
                case FileType.AAA:
                    result =  "aaa";
                    break;
                case FileType.ProductDevelopment:
                    result = "pd";
                    break;
                default:
                    var errmsg = $"Unexpected value {self}";
                    throw new ArgumentException(errmsg);
            }
            return result;
        }

        public static FileType ToFileType(this string self)
        {
            FileType result;
            switch (self.ToLower())
            {
                case "standard":
                    result = FileType.Standard;
                    break;
                case "aaa":
                    result = FileType.AAA;
                    break;
                case "pd":
                    result = FileType.ProductDevelopment;
                    break;
                default:
                    var errmsg = $"Unexpected value {self}";
                    throw new ArgumentException(errmsg);
            }
            return result;
        }

        public static bool IsExists(this FileType self)
        {
            return Enum.IsDefined(typeof(FileType), self);
        }
    }
}
