using System;
using System.Collections.Generic;
using System.Text;

namespace CSVFileReceiver
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
            switch (self)
            {
                case AfterProcess.Move:
                    return "move";
                case AfterProcess.Delete:
                    return "delete";
                default:
                    throw new ArgumentException($"Unexpected value {self}");
            }
        }

        public static AfterProcess ToAfterProcess(this string self)
        {
            switch (self.ToLower())
            {
                case "move":
                    return AfterProcess.Move;
                case "delete":
                    return AfterProcess.Delete;
                default:
                    throw new ArgumentException($"Unexpected value {self}");
            }
        }

    }
}
