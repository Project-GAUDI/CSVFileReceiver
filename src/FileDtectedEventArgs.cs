using System;
using System.IO;

namespace CSVFileReceiver
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
