using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TICO.GAUDI.Commons;

namespace IotedgeV2CSVFileReceiver
{
    class FileWatcher : IDisposable
    {
        public event AsyncEventHandler<FileDtectedEventArgs> OnFileDetected;

        private ILogger MyLogger { get; }

        private FileSystemWatcher MyWatcher { get; set; }

        private string InputPath { get; }

        static bool IncludeSubFolder { get; set; } = false;

        private string SortKey { get; }

        private string SortOrder { get; }

        private bool IsStarting { get; set; } = false;

        private bool IsSearching { get; set; } = false;

        private bool IsSearchWaiting { get; set; } = false;

        private HashSet<string> ExcutingFiles { get; } = new HashSet<string>();

        private object SearchLockObject = new object();

        public FileWatcher(string inputPath, bool includeSubFolder, string sortKey, string sortOrder)
        {
            MyLogger = LoggerFactory.GetLogger(this.GetType());
            InputPath = inputPath;
            IncludeSubFolder = includeSubFolder;
            SortKey = sortKey;
            SortOrder = sortOrder;
        }

        public void Start()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: Start");

            this.IsSearching = false;
            this.IsSearchWaiting = false;
            this.IsStarting = true;

            // 開始時点で存在する入力対象ファイルを捜索
            Task.Run(async () =>
            {
                try
                {
                    await SearchInputFileCoreAsync();
                }
                catch (Exception ex)
                {
                    // 非同期処理内部で発生したエラーはログを出力して握りつぶす
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"SearchInputFileCoreAsync is failed. Exception:{ex}", true);
                }
            });

            // フォルダ監視を開始
            MyWatcher = new FileSystemWatcher(InputPath);
            MyWatcher.Created += MyWatcher_Created;
            MyWatcher.Renamed += MyWatcher_Created;
            MyWatcher.EnableRaisingEvents = true;
            MyWatcher.IncludeSubdirectories = IncludeSubFolder;

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: Start");
        }

        public void Dispose()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: Dispose");

            if (MyWatcher != null)
            {
                MyWatcher.EnableRaisingEvents = false;
                MyWatcher.Created -= MyWatcher_Created;
                MyWatcher.Renamed -= MyWatcher_Created;
                MyWatcher.Dispose();
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: Dispose");
        }

        public async Task Stop()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: Stop");

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

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: Stop");
        }

        public delegate Task AsyncEventHandler<FileDtectedEventArgs>(object sender, FileDtectedEventArgs e);

        private void MyWatcher_Created(object sender, FileSystemEventArgs e)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: MyWatcher_Created");

            Task.Run(async () =>
            {
                try
                {
                    await SearchInputFileAsync();
                }
                catch (Exception ex)
                {
                    // 非同期処理内部で発生したエラーはログを出力して握りつぶす
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"MyWatcher_Created event is failed. Exception:{ex}", true);
                }
            });

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: MyWatcher_Created");
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
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: SearchInputFileAsync");

            IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine();
            if (ApplicationStateChangeResult.Success == await appEngine.SetApplicationRunningAsync())
            {
                await SearchInputFileCoreAsync();
                if (ApplicationStateChangeResult.Success != await appEngine.UnsetApplicationRunningAsync())
                {
                    var errmsg = $"UnsetApplicationRunningAsync is not 'Success'.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SearchInputFileAsync caused by {errmsg}");
                    return;
                }
            }
            else
            {
                var errmsg = $"SetApplicationRunningAsync is not 'Success'.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SearchInputFileAsync caused by {errmsg}");
                return;
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: SearchInputFileAsync");
        }

        private async Task  SearchInputFileCoreAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: SearchInputFileCoreAsync");

            // 他のプロセスが実行中であれば待機する（待機しているプロセスが既にいる場合は終了）
            while (true)
            {
                bool isFileWatcherRunning = IsRunning();
                if (!isFileWatcherRunning)
                {
                    var exitmsg = $"isFileWatcherRunning = {isFileWatcherRunning}.";
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SearchInputFileCoreAsync caused by {exitmsg}");
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
                        var exitmsg = $"IsSearchWaiting = {IsSearchWaiting}.";
                        MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SearchInputFileCoreAsync caused by {exitmsg}");
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
                SearchOption searchOpt = SearchOption.TopDirectoryOnly;
                if (IncludeSubFolder)
                {
                    searchOpt= SearchOption.AllDirectories;
                }
                FileInfo[] inputFiles = inputDir.GetFiles("*", searchOpt);
                
                if (inputFiles == null || inputFiles.Length <= 0)
                {
                    var exitmsg = $"no input files.";
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SearchInputFileCoreAsync caused by {exitmsg}");
                    return;
                }

                // 設定した順序にソート
                Array.Sort<FileInfo>(inputFiles, Compare);
                foreach (var inputFile in inputFiles)
                {
                    bool isFileWatcherRunning = IsRunning();
                    if (!isFileWatcherRunning)
                    {
                        var exitmsg = $"isFileWatcherRunning = {isFileWatcherRunning}.";
                        MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SearchInputFileCoreAsync caused by {exitmsg}");
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

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: SearchInputFileCoreAsync");
        }

        private async Task ExecuteLoadFileAsync(FileInfo inputFile)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: ExecuteLoadFileAsync");
            bool isFileWatcherRunning = IsRunning();
            if (!isFileWatcherRunning)
            {
                var exitmsg = $"isFileWatcherRunning = {isFileWatcherRunning}.";
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: ExecuteLoadFileAsync caused by {exitmsg}");
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
                        MyLogger.WriteLog(ILogger.LogLevel.INFO, $"File \"{inputFile.Name}\" detected.");
                        await OnFileDetected(this, new FileDtectedEventArgs(inputFile));
                    }
                    catch (Exception ex)
                    {
                        MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"OnFileDetected event is failed. Exception:{ex}", true);
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

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: ExecuteLoadFileAsync");
        }
        
        /// <summary>
        /// スケジューラーが動作中かつ終了処理中でないかチェック
        /// </summary>
        internal bool IsRunning()
        {
            IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine();
            bool IsTerminating = appEngine.IsTerminating();
            bool ret = IsStarting && !IsTerminating;
            if (!ret)
            {
                MyLogger.WriteLog(ILogger.LogLevel.DEBUG, $"IsRunning result = {ret}, IsStarting = {IsStarting} ,IsTerminating = {IsTerminating}.");
            }
            return ret;
        }
    }
}
