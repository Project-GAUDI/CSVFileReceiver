using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TICO.GAUDI.Commons;

namespace IotedgeV2CSVFileReceiver
{
    /// <summary>
    /// Application Main class
    /// </summary>
    internal class MyApplicationMain : IApplicationMain
    {
        static ILogger MyLogger { get; } = LoggerFactory.GetLogger(typeof(MyApplicationMain));
        private int _disposed = 0; // 0: false, 1: true

        const string DP_INFO_KEY = "info";
        const string DP_DEFAULT_INFO_KEY = "default_info";
        const string DP_MAX_FILE_SIZE_KEY = "max_file_size";
        const long DEFAULT_MAX_FILE_SIZE = -1;
        const double SIZE_CHECK_THRESHOLD = 0.9;
        const long JSON_OVERHEAD = 16; // {"RecordList":[]}

        static string InputPath { get; set; } = null;

        static string BackupPath { get; set; } = null;

        static string ErrorPath { get; set; } = null;

        static bool IncludeSubFolder { get; set; } = false;

        static string SortKey { get; set; } = null;

        static string SortOrder { get; set; } = null;

        static int MaximumRetryCount { get; set; } = 5;

        static int RetryInterval { get; set; } = 5000;

        static int WaitTime { get; set; } = 0;

        static List<TargetInfo> TargetInfos { get; set; }

        static FileWatcher MyWatcher { get; set; }

        static bool backupEnabled = false;

        /// <summary>
        /// リソース解放
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: DisposeAsync");

            // 2重実行防止
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: Already disposed, skipping DisposeAsync");
                return;
            }

            try
            {
                // ここでApplicationMainのリソース解放処理を行う
                // 非同期リソースがある場合は適切に解放する
                // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝

                await Task.CompletedTask; // 非同期リソース解放処理をここに記述

                // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝
            }
            catch (Exception ex)
            {
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"Error in DisposeAsync: {ex.Message}", true);
                // 例外は再スローしない（Disposeでは通常例外を抑制）
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: DisposeAsync");
        }
        /// <summary>
        /// アプリケーション初期化					
        /// システム初期化前に呼び出される
        /// </summary>
        /// <returns></returns>
        public async Task<bool> InitializeAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: InitializeAsync");

            // ここでApplicationMainの初期化処理を行う。
            // 通信は未接続、DesiredPropertiesなども未取得の状態
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝
            bool retStatus = true;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            try
            {
                // モジュール固有の環境変数
                InputPath = Util.GetEnvironmentVariable<string>("InputPath", true);
                if (!Directory.Exists(InputPath))
                {
                    var errmsg = $"Environment InputPath[{InputPath}] does not exist.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                    throw new Exception(errmsg);
                }
                BackupPath = Util.GetEnvironmentVariable<string>("BackupPath", false, null);
                if (BackupPath == null)
                {
                    MyLogger.WriteLog(ILogger.LogLevel.INFO, $"BackupPath is not set.");
                }
                else if (Directory.Exists(BackupPath))
                {
                    backupEnabled = true;
                }
                ErrorPath = Util.GetEnvironmentVariable<string>("ErrorPath", true);
                if (!Directory.Exists(ErrorPath))
                {
                    var errmsg = $"Environment ErrorPath[{ErrorPath}] does not exist.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                    throw new Exception(errmsg);
                }

                MaximumRetryCount = Util.GetEnvironmentVariable<int>("MaximumRetryCount", false, 5, condition: (value) => value >= 1);
                RetryInterval = Util.GetEnvironmentVariable<int>("RetryInterval", false, 5000, condition: (value) => value >= 0);
                WaitTime = Util.GetEnvironmentVariable<int>("WaitTime", false, 0, condition: (value) => value >= 0);
                IncludeSubFolder = Util.GetEnvironmentVariable<bool>("IncludeSubFolder", false, false);
                SortKey = Util.GetEnvironmentVariable("SortKey", false, "Name", condition: (value) => value.Equals("Name") || value.Equals("Date"));
                SortOrder = Util.GetEnvironmentVariable("SortOrder", false, "Asc", condition: (value) => value.Equals("Asc") || value.Equals("Desc"));
            }
            catch (Exception ex)
            {
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: InitializeAsync caused by {ex.Message}");
                retStatus = false;
                return retStatus;
            }
            await Task.CompletedTask;
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: InitializeAsync");
            return retStatus;
        }

        /// <summary>
        /// アプリケーション起動処理					
        /// システム初期化完了後に呼び出される
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StartAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: StartAsync");

            // ここでApplicationMainの起動処理を行う。
            // 通信は接続済み、DesiredProperties取得済みの状態
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝
            bool retStatus = true;

            MyWatcher = new FileWatcher(InputPath, IncludeSubFolder, SortKey, SortOrder);
            MyWatcher.OnFileDetected += MyWatcher_OnFileDetected;
            MyWatcher.Start();

            await Task.CompletedTask;
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: StartAsync");
            return retStatus;
        }

        /// <summary>
        /// アプリケーション解放。					
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TerminateAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: TerminateAsync");

            // ここでApplicationMainの終了処理を行う。
            // アプリケーション終了時や、
            // DesiredPropertiesの更新通知受信後、
            // 通信切断時の回復処理時などに呼ばれる。
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝
            bool retStatus = true;

            if (MyWatcher != null)
            {
                await MyWatcher.Stop();
                MyWatcher.Dispose();
                MyWatcher = null;
            }
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: TerminateAsync");
            return retStatus;
        }


        /// <summary>
        /// DesiredPropertis更新コールバック。					
        /// </summary>
        /// <param name="desiredProperties">DesiredPropertiesデータ。JSONのルートオブジェクトに相当。</param>
        /// <returns></returns>
        public async Task<bool> OnDesiredPropertiesReceivedAsync(JObject desiredProperties)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: OnDesiredPropertiesReceivedAsync");

            // DesiredProperties更新時の反映処理を行う。
            // 必要に応じて、メンバ変数への格納等を実施。
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝
            bool retStatus = true;

            MyLogger.WriteLog(ILogger.LogLevel.INFO, "Reading desired properties.");
            TargetInfos = new List<TargetInfo>();
            try
            {
                var defaultInfo = Util.GetDesiredProperty<JObject>(desiredProperties, DP_DEFAULT_INFO_KEY, false, new JObject());
                var maxFileSize = Util.GetDesiredProperty<double>(defaultInfo, DP_MAX_FILE_SIZE_KEY, false, DEFAULT_MAX_FILE_SIZE, condition: (value) => value == -1 || (value >= 1 && value == Math.Floor(value) && value <= long.MaxValue / 1_000_000L));
                var defaultMaxFileSize = (long)maxFileSize;
                for (int i = 1; desiredProperties.ContainsKey(DP_INFO_KEY + i.ToString("D")); i++)
                {
                    string key = DP_INFO_KEY + i.ToString("D");
                    JObject jobj = Util.GetDesiredProperty<JObject>(desiredProperties, key, true);
                    var target = TargetInfo.CreateInstance(jobj, backupEnabled, MyLogger, defaultMaxFileSize);
                    TargetInfos.Add(target);
                    if (MyLogger.IsLogLevelToOutput(ILogger.LogLevel.INFO))
                    {
                        target.OutputSettingValues(MyLogger, i);
                    }
                }
                if (TargetInfos.Count == 0)
                {
                    var msg = "DesiredProperties is empty.";
                    throw new Exception(msg);
                }
            }
            catch (Exception ex)
            {
                var errmsg = $"OnDesiredPropertiesReceivedAsync failed.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg} {ex}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: caused by {errmsg}");
                retStatus = false;
                return retStatus;
            }


            await Task.CompletedTask;
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: OnDesiredPropertiesReceivedAsync");

            return retStatus;
        }

        // 監視フォルダ配下のファイルの走査、ファイル検出時の実処理
        private static async Task MyWatcher_OnFileDetected(object sender, FileDtectedEventArgs e)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: MyWatcher_OnFileDetected");
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"TargetInfos count {TargetInfos.Count}");

            var file = e.InputFile;
            var cnt = 0;
            foreach (var info in TargetInfos)
            {
                cnt++;
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"TargetInfo No{cnt}");
                if (Regex.IsMatch(file.Name, info.Filter))
                {
                    string fileName = file.Name;
                    try
                    {
                        await SendMessageAsync(file, info);
                    }
                    catch (Exception)
                    {
                        await ErrorSendEventAsync(info.ErrorOutputName, fileName);
                        await MoveFileAsync(file, ErrorPath);

                        var errmsg = $"Exeption from SendMessageAsync.";
                        MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: MyWatcher_OnFileDetected caused by {errmsg}");
                        throw;
                    }
                    switch (info.AfterProcess)
                    {
                        case AfterProcess.Delete:
                            await DeleteFileAsync(file);
                            break;
                        case AfterProcess.Move:
                            await MoveFileAsync(file, BackupPath);
                            break;
                    }
                    break;
                }
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: MyWatcher_OnFileDetected");
        }

        /// MyWatcher_OnFileDetectedから呼び出しのみ、Set/Unset不要
        static async Task SendMessageAsync(FileInfo file, TargetInfo info)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: SendMessageAsync");

            var filenameProps = new Dictionary<string, string>();
            if (info.FilenamePropertiesEnabled)
            {
                string pattern = "^[0-9a-zA-Z]{3}_(?<format>[0-9a-zA-Z]{3,4})_(?<country>[0-9a-zA-Z]{2})(?<company>[0-9a-zA-Z]{2})(?<factory>[0-9a-zA-Z]{3})(?<data_type>[0-9a-zA-Z]{3})(?<free_area>[0-9a-zA-Z]{5})_.+\\.(C|c)(S|s)(V|v)$";
                Regex reg = new Regex(pattern);
                Match m = reg.Match(file.Name);
                if (m.Success)
                {
                    filenameProps.Add("country", m.Groups["country"].Value);
                    filenameProps.Add("company", m.Groups["company"].Value);
                    filenameProps.Add("factory", m.Groups["factory"].Value);
                    filenameProps.Add("data_type", m.Groups["data_type"].Value);
                    filenameProps.Add("free_area", m.Groups["free_area"].Value);
                    filenameProps.Add("format", m.Groups["format"].Value);
                }
                else
                {
                    var errmsg = $"ファイル名({file.Name})がGAUDI標準のCSVファイル命名規約「送信先No.(3桁)_フォーマットNo.(3桁または4桁)_データ取得場所(15桁)_可変データ(N桁).csv」に従っていません.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, errmsg);
                    throw new Exception(errmsg);
                }
            }

            file.Refresh();
            if (file.Exists && info.FileSizeLimitBytes > 0 && file.Length > info.FileSizeLimitBytes)
            {
                var errmsg = $"File '{file.Name}' size ({file.Length} bytes) exceeds the configured maximum ({info.FileSizeLimitBytes} bytes).";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, errmsg);
                throw new Exception(errmsg);
            }

            List<List<string>> lines = null;
            lines = await MessageFactory.GetCsvRecords(MaximumRetryCount, RetryInterval, file, info, MyLogger);

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"get records count:{lines.Count}");
            var firstDataProp = new Dictionary<string, string>();
            if (info.SendHeaderEnabled || info.EofEnabled || info.ColumnsLine.HasValue)
            {
                if (!lines.Any())
                {
                    var exitmsg = $"input file is empty.";
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SendMessageAsync caused by {exitmsg}");
                    return;
                }
            }

            // ファイル行数に対するバリデーション
            int totalLines = lines.Count;
            if (info.SendHeaderEnabled && totalLines < info.HeaderStartLine)
            {
                var errmsg = $"File has {totalLines} lines, but header_start_line is {info.HeaderStartLine}.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, errmsg);
                throw new Exception(errmsg);
            }
            if (info.SendHeaderEnabled && totalLines < info.HeaderEndLine)
            {
                var errmsg = $"File has {totalLines} lines, but header_end_line is {info.HeaderEndLine}.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, errmsg);
                throw new Exception(errmsg);
            }
            if (info.ColumnsLine.HasValue && totalLines < info.ColumnsLine.Value)
            {
                var errmsg = $"File has {totalLines} lines, but columns_line is {info.ColumnsLine.Value}.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, errmsg);
                throw new Exception(errmsg);
            }
            if (totalLines > 0 && totalLines < info.DataStartLine)
            {
                var errmsg = $"File has {totalLines} lines, but data_start_line is {info.DataStartLine}.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, errmsg);
                throw new Exception(errmsg);
            }

            int startIndex = info.DataStartLine - 1;
            if ((0 <= startIndex) && (startIndex <= lines.Count - 1))
            {
                var firstData = lines[startIndex];
                foreach (var dataProperty in info.DataProperties)
                {
                    firstDataProp.Add(dataProperty.Name, firstData[dataProperty.Column - 1]);
                }
            }



            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Processing lines_count:'{lines.Count}'");

            var filename = file.Name;
            var header = new List<string>();
            var recordsData = MessageFactory.GetRecordDataFromCsvData(lines, info, MyLogger);

            // columns プロパティ値を生成
            string headerRowProperty = null;
            if (recordsData.ColumnsRecord != null)
            {
                headerRowProperty = MessageFactory.CreateHeaderRowPropertyValue(recordsData.ColumnsRecord);
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"send_max_records: {info.SendMaximumRecords}");
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"max_message_size: {info.MaxMessageSize}");
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"message_size_limit_bytes: {info.MessageSizeLimitBytes}");
            var jsonMsg = new JsonMessage() { RecordList = new List<JsonMessage.RecordInfo>() };
            var maxRecords = info.SendMaximumRecords;
            var maxMessageSize = info.MessageSizeLimitBytes;
            if (!recordsData.HeaderRecords.Any()) recordsData.HeaderRecords.Add(filename);
            int cnt = 0;
            var rownum = 0;
            int sendcnt = 0;
            int rowtotal = recordsData.DataRecords.Count;
            var dataProps = new Dictionary<string, string>();

            // プロパティオーバーヘッドを見積もり
            long propertyOverhead = maxMessageSize > 0 ? EstimatePropertyOverhead(filename, rowtotal, firstDataProp, filenameProps, headerRowProperty) : 0;
            long estimatedSize = JSON_OVERHEAD + propertyOverhead;  
            long messageSizeLimit = maxMessageSize;  
            if (info.SendHeaderEnabled && recordsData.HeaderRecordsStandard.Any())
            {
                foreach (var record in recordsData.HeaderRecordsStandard)
                {
                    cnt++;
                    if (rownum == 0)
                    {
                        dataProps = firstDataProp;
                        rownum = cnt;
                    }
                    var recordHeader = info.RecordMetadataEnabled ? new List<string>(recordsData.HeaderRecords) { cnt.ToString() } : null;
                    var recordInfo = new JsonMessage.RecordInfo() { RecordHeader = recordHeader, RecordData = record.Cast<object>().ToList() };
                    jsonMsg.RecordList.Add(recordInfo);

                    // サイズ見積もりと検証
                    bool shouldSendBySize = false;
                    if (maxMessageSize > 0)
                    {
                        var recordBytes = JsonMessage.SerializeRecordInfoByte(recordInfo);
                        long recordSize = recordBytes != null ? recordBytes.Length : 0;
                        estimatedSize += recordSize + 1; // +1 for comma separator

                        if (estimatedSize >= (long)(messageSizeLimit * SIZE_CHECK_THRESHOLD))
                        {
                            var actualBytes = JsonMessage.SerializeJsonMessageByte(jsonMsg);
                            long actualSize = actualBytes != null ? actualBytes.Length : 0;
                            if (actualSize + propertyOverhead > messageSizeLimit)
                            {
                                if (jsonMsg.RecordList.Count == 1)  
                                {  
                                    var errmsg = $"Single record exceeds message size limit ({actualSize + propertyOverhead} bytes > {messageSizeLimit} bytes). line:{cnt}";  
                                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, errmsg);  
                                    throw new Exception(errmsg);  
                                }
                                // 最後のレコードを次バッチに回す
                                jsonMsg.RecordList.RemoveAt(jsonMsg.RecordList.Count - 1);
                                shouldSendBySize = true;
                            }
                        }
                    }

                    if ((0 < maxRecords && jsonMsg.RecordList.Count == maxRecords) || shouldSendBySize)
                    {
                        using (var msg = MessageFactory.CreateMessage(jsonMsg, filename, rownum, rowtotal, dataProps, filenameProps))
                        {
                            var msgSize = msg.GetBodyStream().Length;
                            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"message size:{msgSize}");
                            IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine();
                            await appEngine.SendMessageAsync(info.OutputName, msg);
                            await WaitAfterSendAsync();
                            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"send {jsonMsg.RecordList.Count} records.");
                        }

                        sendcnt++;
                        // 送信するJsonMessageリストを初期化
                        jsonMsg.RecordList.Clear();
                        jsonMsg.RecordList = new List<JsonMessage.RecordInfo>();
                        estimatedSize = JSON_OVERHEAD + propertyOverhead;
                        rownum = 0;

                        // サイズ超過で次バッチに回したレコードを再追加
                        if (shouldSendBySize)
                        {
                            dataProps = firstDataProp;
                            rownum = cnt;
                            jsonMsg.RecordList.Add(recordInfo);
                            var readdedBytes = JsonMessage.SerializeRecordInfoByte(recordInfo);
                            estimatedSize += (readdedBytes != null ? readdedBytes.Length : 0) + 1;
                        }
                    }
                }
            }
            foreach (var record in recordsData.DataRecords)
            {
                cnt++;
                if (rownum == 0)
                {
                    dataProps = new Dictionary<string, string>();
                    foreach (var dataProperty in info.DataProperties)
                    {
                        if (dataProperty.Get_From == GetFrom.File)
                        {
                            dataProps.Add(dataProperty.Name, firstDataProp[dataProperty.Name]);
                        }
                        else
                        {
                            dataProps.Add(dataProperty.Name, record[dataProperty.Column - 1]);
                        }
                    }

                    rownum = cnt;
                }
                var recordHeader = info.RecordMetadataEnabled ? new List<string>(recordsData.HeaderRecords) { cnt.ToString() } : null;
                var recordInfo = new JsonMessage.RecordInfo() { RecordHeader = recordHeader, RecordData = record.Cast<object>().ToList() };
                jsonMsg.RecordList.Add(recordInfo);

                // サイズ見積もりと検証
                bool shouldSendBySize = false;
                if (maxMessageSize > 0)
                {
                    var recordBytes = JsonMessage.SerializeRecordInfoByte(recordInfo);
                    long recordSize = recordBytes != null ? recordBytes.Length : 0;
                    estimatedSize += recordSize + 1; // +1 for comma separator

                    if (estimatedSize >= (long)(messageSizeLimit * SIZE_CHECK_THRESHOLD))
                    {
                        var actualBytes = JsonMessage.SerializeJsonMessageByte(jsonMsg);
                        long actualSize = actualBytes != null ? actualBytes.Length : 0;
                        if (actualSize + propertyOverhead > messageSizeLimit)
                        {
                            if (jsonMsg.RecordList.Count == 1)  
                            {  
                                var errmsg = $"Single record exceeds message size limit ({actualSize + propertyOverhead} bytes > {messageSizeLimit} bytes). line:{cnt}";  
                                MyLogger.WriteLog(ILogger.LogLevel.ERROR, errmsg);  
                                throw new Exception(errmsg);  
                            }
                            // 最後のレコードを次バッチに回す
                            jsonMsg.RecordList.RemoveAt(jsonMsg.RecordList.Count - 1);
                            shouldSendBySize = true;
                        }
                    }
                }

                if ((0 < maxRecords && jsonMsg.RecordList.Count == maxRecords) || shouldSendBySize)
                {
                    using (var msg = MessageFactory.CreateMessage(jsonMsg, filename, rownum, rowtotal, dataProps, filenameProps))
                    {
                        if (headerRowProperty != null) msg.SetProperty("columns", headerRowProperty);
                        var msgSize = msg.GetBodyStream().Length;
                        MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"message size:{msgSize}");
                        IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine();
                        await appEngine.SendMessageAsync(info.OutputName, msg);
                        await WaitAfterSendAsync();
                        MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"send {jsonMsg.RecordList.Count} records.");
                    }
                    sendcnt++;

                    // 送信するJsonMessageリストを初期化
                    jsonMsg.RecordList.Clear();
                    jsonMsg.RecordList = new List<JsonMessage.RecordInfo>();
                    estimatedSize = JSON_OVERHEAD + propertyOverhead;
                    rownum = 0;

                    // サイズ超過で次バッチに回したレコードを再追加
                    if (shouldSendBySize)
                    {
                        dataProps = new Dictionary<string, string>();
                        foreach (var dataProperty in info.DataProperties)
                        {
                            if (dataProperty.Get_From == GetFrom.File)
                            {
                                dataProps.Add(dataProperty.Name, firstDataProp[dataProperty.Name]);
                            }
                            else
                            {
                                dataProps.Add(dataProperty.Name, record[dataProperty.Column - 1]);
                            }
                        }
                        rownum = cnt;
                        jsonMsg.RecordList.Add(recordInfo);
                        var readdedBytes = JsonMessage.SerializeRecordInfoByte(recordInfo);
                        estimatedSize += (readdedBytes != null ? readdedBytes.Length : 0) + 1;
                    }
                }
            }
            if (jsonMsg.RecordList.Any())
            {
                using (var msg = MessageFactory.CreateMessage(jsonMsg, filename, rownum, rowtotal, dataProps, filenameProps))
                {
                    if (headerRowProperty != null) msg.SetProperty("columns", headerRowProperty);
                    var msgSize = msg.GetBodyStream().Length;
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"message size:{msgSize}");
                    IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine();
                    await appEngine.SendMessageAsync(info.OutputName, msg);
                    await WaitAfterSendAsync();
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"send {jsonMsg.RecordList.Count} records.");
                }
                sendcnt++;
            }

            if (info.EofEnabled)
            {
                using (var eof = new IotMessage())
                {
                    eof.SetProperty("filename", filename);
                    eof.SetProperty("type", "eof");
                    eof.SetProperty("row_count", cnt.ToString("D"));
                    eof.SetProperty("row_total", rowtotal.ToString("D"));
                    eof.SetProperties(firstDataProp);
                    eof.SetProperties(filenameProps);
                    if (headerRowProperty != null) eof.SetProperty("columns", headerRowProperty);

                    IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine();
                    await appEngine.SendMessageAsync(info.OutputName, eof);
                    await WaitAfterSendAsync();
                    sendcnt++;
                    if (MyLogger.IsLogLevelToOutput(ILogger.LogLevel.TRACE))
                    {
                        MyLogger.WriteLog(ILogger.LogLevel.INFO, $"Send EOF Message of {file.Name}.");
                    }
                }
            }

            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"Message send completed. '{file.Name}', {sendcnt} messages, {cnt} records, '{info.OutputName}'");

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: SendMessageAsync");
        }

        /// <summary>
        /// メッセージプロパティのオーバーヘッド（UTF-8バイト長）を見積もる
        /// </summary>
        static long EstimatePropertyOverhead(string filename, int rowtotal, Dictionary<string, string> dataProps, Dictionary<string, string> filenameProps, string headerRowProperty)
        {
            long overhead = 0;
            // 固定プロパティ: filename, row_number, row_total
            overhead += Encoding.UTF8.GetByteCount("filename") + Encoding.UTF8.GetByteCount(filename);
            overhead += Encoding.UTF8.GetByteCount("row_number") + Encoding.UTF8.GetByteCount(rowtotal.ToString("D")); // row_numberの最大桁数 ≒ rowtotal桁数
            overhead += Encoding.UTF8.GetByteCount("row_total") + Encoding.UTF8.GetByteCount(rowtotal.ToString("D"));

            // data_property
            if (dataProps != null)
            {
                foreach (var kv in dataProps)
                {
                    overhead += Encoding.UTF8.GetByteCount(kv.Key) + Encoding.UTF8.GetByteCount(kv.Value ?? string.Empty);
                }
            }

            // filename_properties
            if (filenameProps != null)
            {
                foreach (var kv in filenameProps)
                {
                    overhead += Encoding.UTF8.GetByteCount(kv.Key) + Encoding.UTF8.GetByteCount(kv.Value ?? string.Empty);
                }
            }

            // columns property
            if (headerRowProperty != null)
            {
                overhead += Encoding.UTF8.GetByteCount("columns") + Encoding.UTF8.GetByteCount(headerRowProperty);
            }

            return overhead;
        }

        /// MyWatcher_OnFileDetectedから呼び出しのみ、Set/Unset不要
        static async Task DeleteFileAsync(FileInfo file)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: DeleteFileAsync");

            // if ((int)Logger.OutputLogLevel <= (int)Logger.LogLevel.TRACE)
            //     MyLogger.WriteLog(Logger.LogLevel.TRACE, $"DeleteFileAsync called.  file={file.Name}");

            for (int i = 0; i < MaximumRetryCount; i++)
            {
                try
                {
                    file.Delete();
                    break;
                }
                catch (Exception ex) when
                (ex is IOException
                || ex is System.Security.SecurityException
                || ex is UnauthorizedAccessException)
                {
                    MyLogger.WriteLog(ILogger.LogLevel.WARN, $"Delete file[{file.Name}] failed[{i + 1}/{MaximumRetryCount}]: {ex.Message}");
                    if ((i + 1) == MaximumRetryCount)
                    {
                        throw;
                    }
                    await Task.Delay(RetryInterval);
                }
                catch (Exception ex)
                {
                    var errmsg = $"Delete file[{file.Name}] failed.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg} {ex}", true);
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: DeleteFileAsync caused by {errmsg}");
                    throw;
                }
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: DeleteFileAsync");
        }

        /// MyWatcher_OnFileDetectedから呼び出しのみ、Set/Unset不要
        static async Task MoveFileAsync(FileInfo file, string DestPath)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: MoveFileAsync");

            // if ((int)Logger.OutputLogLevel <= (int)Logger.LogLevel.TRACE)
            //     MyLogger.WriteLog(Logger.LogLevel.TRACE, $"MoveFileAsync called.  file={file.Name}");

            var path = Path.Combine(DestPath, file.Name);
            if (File.Exists(path))
            {
                path += "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            }

            for (int i = 0; i < MaximumRetryCount; i++)
            {
                try
                {
                    file.MoveTo(path);
                    break;
                }
                catch (Exception ex) when
                (ex is DirectoryNotFoundException
                || ex is FileNotFoundException
                || ex is IOException
                || ex is System.Security.SecurityException
                || ex is UnauthorizedAccessException)
                {
                    MyLogger.WriteLog(ILogger.LogLevel.WARN, $"Move file[{file.FullName}] to [{path}] failed[{i + 1}/{MaximumRetryCount}]: {ex.Message}");
                    if ((i + 1) == MaximumRetryCount)
                    {
                        throw;
                    }
                    await Task.Delay(RetryInterval);
                }
                catch (Exception ex)
                {
                    var errmsg = $"Move file[{file.Name}] to [{path}] failed.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg} {ex}", true);
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: caused by {errmsg}");
                    throw;
                }
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: MoveFileAsync");
        }

        /// <summary>
        /// エラー発生時のメッセージ送信処理（空のメッセージを送信する）
        /// エラーファイル名は
        /// </summary>
        /// <param name="errOutputName"></param>
        /// <returns></returns>
        /// MyWatcher_OnFileDetectedから呼び出しのみ、Set/Unset不要
        private static async Task ErrorSendEventAsync(string errOutputName, string fileName)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: ErrorSendEventAsync");

            try
            {
                if (string.IsNullOrEmpty(errOutputName))
                {
                    var exitmsg = $"errOutputName is null or empty.";
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: ErrorSendEventAsync caused by {exitmsg}");
                    return;
                }
                var msg = new IotMessage("{}");
                msg.SetProperty("filename", fileName);
                IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine();
                await appEngine.SendMessageAsync(errOutputName, msg);
                await WaitAfterSendAsync();
                MyLogger.WriteLog(ILogger.LogLevel.INFO, $"ErrorSendEventAsync : sent completed. output_name='{errOutputName}'");
            }
            catch (Exception ex)
            {
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"ErrorSendEventAsync failed. {ex}", true);
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: ErrorSendEventAsync");
        }

        /// <summary>
        /// WaitTimeで指定した時間待機する
        /// </summary>
        /// <returns></returns>
        /// SendMessageAsyncおよびErrorSendEventAsyncから呼び出しのみ、Set/Unset不要
        private static async Task WaitAfterSendAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: WaitAfterSendAsync");

            if (0 < WaitTime)
            {
                await System.Threading.Tasks.Task.Delay(WaitTime);
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: WaitAfterSendAsync");
        }
    }
}