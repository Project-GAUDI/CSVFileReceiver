using System;
using System.Collections.Generic;
using System.Text;

namespace CSVFileReceiver
{
    public class DataProperty
    {
        public string Name { get; }
        public int Column { get; }

        public GetFrom Get_From { get; }

        public DataProperty(string name, int column, GetFrom getFrom=GetFrom.Message)
        {
            Name = name;
            Column = column;
            Get_From = getFrom;
        }
    }
}
