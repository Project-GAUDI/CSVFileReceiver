using System;
using System.IO;

namespace IotedgeV2CSVFileReceiver
{
    class FileDtectedEventArgs : EventArgs
    {
        public FileInfo InputFile { get; }

        public FileDtectedEventArgs(FileInfo fileInfo) : base()
        {
            InputFile = fileInfo;
        }
    }
}
