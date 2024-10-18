using System;
using System.Collections.Generic;
using System.Text;
using TICO.GAUDI.Commons;

namespace IotedgeV2CSVFileReceiver
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
            string result = null;
            switch (self)
            {
                case GetFrom.Message:
                    result = "message";
                    break;
                case GetFrom.File:
                    result = "file";
                    break;
                default:
                    var errmsg = $"Unexpected value {self}";
                    throw new ArgumentException(errmsg);
            }
            return result;
        }

        public static GetFrom ToGetFrom(this string self)
        {
            GetFrom result;
            switch (self.ToLower())
            {
                case "message":
                    result = GetFrom.Message;
                    break;
                case "file":
                    result = GetFrom.File;
                    break;
                default:
                    var errmsg = $"Unexpected value {self}";
                    throw new ArgumentException(errmsg);
            }
            return result;
        }

    }
}
