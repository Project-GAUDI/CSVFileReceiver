using System;
using System.Collections.Generic;
using System.Text;
using TICO.GAUDI.Commons;

namespace IotedgeV2CSVFileReceiver
{
    public enum AfterProcess
    {
        Move,
        Delete
    }

    public static class AfterProcessExtensions
    {
        public static string ToString(this AfterProcess self)
        {
            string result = null;
            switch (self)
            {
                case AfterProcess.Move:
                    result = "move";
                    break;
                case AfterProcess.Delete:
                    result = "delete";
                    break;
                default:
                    var errmsg = $"Unexpected value {self}";
                    throw new ArgumentException(errmsg);
            }
            return result;
            
        }

        public static AfterProcess ToAfterProcess(this string self)
        {
            AfterProcess result;
            switch (self.ToLower())
            {
                case "move":
                    result = AfterProcess.Move;
                    break;
                case "delete":
                    result = AfterProcess.Delete;
                    break;
                default:
                    var errmsg = $"Unexpected value {self}";
                    throw new ArgumentException(errmsg);
            }
            return result;
        }
    }
}
