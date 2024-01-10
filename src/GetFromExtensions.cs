using System;
using System.Collections.Generic;
using System.Text;

namespace CSVFileReceiver
{
    public enum GetFrom
    {
        Message,
        File
    }

    public static class GetFromExtensions
    {
        public static string ToString(this GetFrom self)
        {
            switch (self)
            {
                case GetFrom.Message:
                    return "message";
                case GetFrom.File:
                    return "file";
                default:
                    throw new ArgumentException($"Unexpected value {self}");
            }
        }

        public static GetFrom ToGetFrom(this string self)
        {
            switch (self.ToLower())
            {
                case "message":
                    return GetFrom.Message;
                case "file":
                    return GetFrom.File;
                default:
                    throw new ArgumentException($"Unexpected value {self}");
            }
        }

    }
}
