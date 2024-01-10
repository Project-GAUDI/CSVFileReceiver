using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TICO.GAUDI.Commons;

namespace CSVFileReceiver
{
    class FileWatcher : IDisposable
    {
        public event AsyncEventHandler<FileDtectedEventArgs> OnFileDetected;

        private Logger MyLogger { get; }

        private FileSystemWatcher MyWatcher { get; set; }

        private string InputPath { get; }

        private string SortKey { get; }

        private string SortOrder { get; }

        private bool IsStarting { get; set; } = false;

        private bool IsSearching { get; set; } = false;

        private bool IsSearchWaiting { get; set; } = false;

        private HashSet<string> ExcutingFiles { get; } = new HashSet<string>();

        private object SearchLockObject = new object();

        public FileWatcher(string inputPath, string sortKey, string sortOrder)
        {
            MyLogger = Logger.GetLogger(this.GetType());
            InputPath = inputPath;
            SortKey = sortKey;
            SortOrder = sortOrder;
        }

        public void Start()
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");

            this.IsSearching = false;
            this.IsSearchWaiting = false;
            this.IsStarting = true;

            // 開始時点で存在する入力対象ファイルを捜索
            Task.Run(async () =>
            {
                try
                {
                    await SearchInputFileAsync();
                }
                catch (Exception exp)
                {
                    // 非同期処理内部で発生したエラーはログを出力して握りつぶす
                    MyLogger.WriteLog(Logger.LogLevel.ERROR, $"SearchInputFileAsync is failed. Exception:{exp}", true);
                }
            });

            // フォルダ監視を開始
            MyWatcher = new FileSystemWatcher(InputPath);
            MyWatcher.Created += MyWatcher_Created;
            MyWatcher.Renamed += MyWatcher_Created;
            MyWatcher.EnableRaisingEvents = true;

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");
        }

        public void Dispose()
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");

            if (MyWatcher != null)
            {
                MyWatcher.EnableRaisingEvents = false;
                MyWatcher.Created -= MyWatcher_Created;
                MyWatcher.Renamed -= MyWatcher_Created;
                MyWatcher.Dispose();
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");
        }

        public async Task Stop()
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: Stop");

            this.IsStarting = false;

            for (int i = 0; i < 30; i++)
            {
                lock (this.ExcutingFiles)
                {
                    if (this.ExcutingFiles.Count <= 0)
                    {
                        break;
                    }
                }
                await Task.Delay(1000);
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: Stop");
        }

        public delegate Task AsyncEventHandler<FileDtectedEventArgs>(object sender, FileDtectedEventArgs e);

        private void MyWatcher_Created(object sender, FileSystemEventArgs e)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");

            Task.Run(async () =>
            {
                try
                {
                    await SearchInputFileAsync();
                }
                catch (Exception exp)
                {
                    // 非同期処理内部で発生したエラーはログを出力して握りつぶす
                    MyLogger.WriteLog(Logger.LogLevel.ERROR, $"MyWatcher_Created event is failed. Exception:{exp}", true);
                }
            });

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");
        }

        private int Compare(FileInfo a, FileInfo b)
        {
            // 使用頻度がかなり高いためTRACEログには残さない
            if (SortKey.Equals("Name"))
            {
                if (SortOrder.Equals("Asc"))
                {
                    return a.Name.CompareTo(b.Name);
                }
                else
                {
                    return b.Name.CompareTo(a.Name);
                }
            }
            else
            {
                if (SortOrder.Equals("Asc"))
                {
                    return a.LastWriteTime.CompareTo(b.LastWriteTime);
                }
                else
                {
                    return b.LastWriteTime.CompareTo(a.LastWriteTime);
                }
            }
        }

        private async Task SearchInputFileAsync()
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: SearchInputFileAsync");

            // 他のプロセスが実行中であれば待機する（待機しているプロセスが既にいる場合は終了）
            while (true)
            {
                if (!this.IsStarting)
                {
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Exit Method: SearchInputFileAsync caused by IsStarting = {IsStarting}.");
                    return;
                }

                lock (this.SearchLockObject)
                {
                    if (!this.IsSearching)
                    {
                        this.IsSearching = true;
                        break;
                    }
                    else if (!this.IsSearchWaiting)
                    {
                        this.IsSearchWaiting = true;
                    }
                    else
                    {
                        MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Exit Method: SearchInputFileAsync caused by IsSearchWaiting = {IsSearchWaiting}.");
                        return;
                    }
                }
                await Task.Delay(1000);
                lock (this.SearchLockObject)
                {
                    this.IsSearchWaiting = false;
                }
            }

            try
            {
                // 入力フォルダのファイル一覧情報を取得
                DirectoryInfo inputDir = new DirectoryInfo(InputPath);
                FileInfo[] inputFiles = inputDir.GetFiles();

                if (inputFiles == null || inputFiles.Length <= 0)
                {
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Exit Method: SearchInputFileAsync caused by no input files.");
                    return;
                }

                // 設定した順序にソート
                Array.Sort<FileInfo>(inputFiles, Compare);
                foreach (var inputFile in inputFiles)
                {
                    if (!this.IsStarting)
                    {
                        MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Exit Method: SearchInputFileAsync caused by IsStarting = {IsStarting}.");
                        return;
                    }
                    // ファイル読込処理を起動
                    await ExecuteLoadFileAsync(inputFile);
                }
            }
            finally
            {
                lock (this.SearchLockObject)
                {
                    this.IsSearching = false;
                }
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: SearchInputFileAsync");
        }

        private async Task ExecuteLoadFileAsync(FileInfo inputFile)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: ExecuteLoadFileAsync");

            if (!this.IsStarting)
            {
                MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Exit Method: ExecuteLoadFileAsync caused by IsStarting = {IsStarting}.");
                return;
            }
            var inputfile_fullname = inputFile.FullName;

            try
            {
                bool istarget = false;
                lock (this.ExcutingFiles)
                {
                    if (!this.ExcutingFiles.Contains(inputfile_fullname))
                    {
                        if (inputFile.Exists)
                        {
                            this.ExcutingFiles.Add(inputfile_fullname);
                            istarget = true;
                        }
                    }
                }

                // 入力対象のファイルが存在する かつ 処理中で無ければ検知イベントを発火
                if (istarget)
                {
                    try
                    {
                        MyLogger.WriteLog(Logger.LogLevel.INFO, $"File \"{inputFile.Name}\" detected.");
                        await OnFileDetected(this, new FileDtectedEventArgs(inputFile));
                    }
                    catch (Exception ex)
                    {
                        MyLogger.WriteLog(Logger.LogLevel.ERROR, $"OnFileDetected event is failed. Exception:{ex}", true);
                    }
                }
            }
            finally
            {
                lock (this.ExcutingFiles)
                {
                    this.ExcutingFiles.Remove(inputfile_fullname);
                }
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: ExecuteLoadFileAsync");
        }

    }
}
