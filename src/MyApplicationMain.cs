using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
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

        const string DP_INFO_KEY = "info";

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

        public void Dispose()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: Dispose");

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: Dispose");
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

            // モジュール固有の環境変数
            InputPath = Environment.GetEnvironmentVariable("InputPath");
            if (string.IsNullOrEmpty(InputPath))
            {
                var errmsg = "Environment InputPath does not set.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: InitializeAsync caused by {errmsg}");
                retStatus = false;
                return retStatus;
            }
            else if (!Directory.Exists(InputPath))
            {
                var errmsg = $"Environment InputPath[{InputPath}] does not exist.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: InitializeAsync caused by {errmsg}");
                retStatus = false;
                return retStatus;
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"InputPath = {InputPath}");

            backupEnabled = false;
            BackupPath = Environment.GetEnvironmentVariable("BackupPath");
            if (string.IsNullOrEmpty(BackupPath))
            {
                MyLogger.WriteLog(ILogger.LogLevel.INFO, "Environment BackupPath does not set. File back up is disabled.");
            }
            else if (Directory.Exists(BackupPath))
            {
                MyLogger.WriteLog(ILogger.LogLevel.INFO, $"BackupPath = {BackupPath}");
                backupEnabled = true;
            }
            else
            {
                var errmsg = $"Environment BackupPath[{BackupPath}] does not exist.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: InitializeAsync caused by {errmsg}");
                retStatus = false;
                return retStatus;
            }

            ErrorPath = Environment.GetEnvironmentVariable("ErrorPath");
            if (string.IsNullOrEmpty(ErrorPath))
            {
                var errmsg = "Environment ErrorPath does not set.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: InitializeAsync caused by {errmsg}");
                retStatus = false;
                return retStatus;
            }
            else if (!Directory.Exists(ErrorPath))
            {
                var errmsg = $"Environment ErrorPath[{ErrorPath}] does not exist.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: InitializeAsync caused by {errmsg}");
                retStatus = false;
                return retStatus;
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"ErrorPath = {ErrorPath}");

            MaximumRetryCount = 5;
            string maxCon = Environment.GetEnvironmentVariable("MaximumRetryCount");
            if (!string.IsNullOrEmpty(maxCon))
            {
                if (int.TryParse(maxCon, out int con) && 1 <= con)
                {
                    MaximumRetryCount = con;
                }
                else
                {
                    MyLogger.WriteLog(ILogger.LogLevel.WARN, $"Environment MaximumRetryCount can't set {maxCon}. Default value ({MaximumRetryCount}) assigned.");
                }
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"MaximumRetryCount = {MaximumRetryCount}");

            RetryInterval = 5000;
            string interval = Environment.GetEnvironmentVariable("RetryInterval");
            if (!string.IsNullOrEmpty(interval))
            {
                if (int.TryParse(interval, out int i) && 0 <= i)
                {
                    RetryInterval = i;
                }
                else
                {
                    MyLogger.WriteLog(ILogger.LogLevel.WARN, $"Environment RetryInterval can't set {interval}. Default value ({RetryInterval}) assigned.");
                }
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"RetryInterval = {RetryInterval}");

            WaitTime = 0;
            string waittime = Environment.GetEnvironmentVariable("WaitTime");
            if (!string.IsNullOrEmpty(waittime))
            {
                if (int.TryParse(waittime, out int wt) && 0 <= wt)
                {
                    WaitTime = wt;
                }
                else
                {
                    MyLogger.WriteLog(ILogger.LogLevel.WARN, $"Environment WaitTime can't set {waittime}. Default value ({WaitTime}) assigned.");
                }
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"WaitTime = {WaitTime}");

            IncludeSubFolder = false;
            string strIncSubFld = Environment.GetEnvironmentVariable("IncludeSubFolder");
            if (!string.IsNullOrEmpty(strIncSubFld))
            {
                if (Boolean.TryParse(strIncSubFld, out bool incSubFld))
                {
                    IncludeSubFolder = incSubFld;
                }
                else
                {
                    MyLogger.WriteLog(ILogger.LogLevel.WARN, $"Environment IncludeSubFolder can't set {strIncSubFld}. Default value ({IncludeSubFolder}) assigned.");
                }
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"IncludeSubFolder = {IncludeSubFolder}");

            SortKey = "Name";
            string key = Environment.GetEnvironmentVariable("SortKey");
            if (!string.IsNullOrEmpty(key))
            {
                if (key.Equals("Name") || key.Equals("Date"))
                {
                    SortKey = key;
                }
                else
                {
                    MyLogger.WriteLog(ILogger.LogLevel.WARN, $"Environment SortKey can't set {key}. Default value ({SortKey}) assigned.");
                }
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"SortKey = {SortKey}");

            SortOrder = "Asc";
            string order = Environment.GetEnvironmentVariable("SortOrder");
            if (!string.IsNullOrEmpty(order))
            {
                if (order.Equals("Asc") || order.Equals("Desc"))
                {
                    SortOrder = order;
                }
                else
                {
                    MyLogger.WriteLog(ILogger.LogLevel.WARN, $"Environment SortOrder can't set {order}. Default value ({SortOrder}) assigned.");
                }
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"SortOrder = {SortOrder}");

            await Task.CompletedTask;
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: InitializeAsync");
            return retStatus;
        }

        /// <summary>
        /// アプリケーション起動処理					
        /// システム初期化完了後に呼び出される
        /// </summary>
        /// <param name=""></param>
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
                for (int i = 1; desiredProperties.ContainsKey(DP_INFO_KEY + i.ToString("D")); i++)
                {
                    string key = DP_INFO_KEY + i.ToString("D");
                    JObject jobj = Util.GetRequiredValue<JObject>(desiredProperties, key);
                    var target = TargetInfo.CreateInstance(jobj, backupEnabled, MyLogger);
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

        /// SearchInputFileAsyncから呼び出しのみ、Set/Unset不要
        static async Task SendMessageAsync(FileInfo file, TargetInfo info)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: SendMessageAsync");

            var filenameProps = new Dictionary<string, string>();
            if (info.FilenamePropertiesEnabled)
            {
                string pattern = "[0-9a-zA-Z]{3}_(?<format>[0-9a-zA-Z]{3,4})_(?<country>[0-9a-zA-Z]{2})(?<company>[0-9a-zA-Z]{2})(?<factory>[0-9a-zA-Z]{3})(?<data_type>[0-9a-zA-Z]{3})(?<free_area>[0-9a-zA-Z]{5})_.+\\.(C|c)(S|s)(V|v)";
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
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SendMessageAsync caused by {errmsg}");
                    throw new Exception(errmsg);
                }
            }

            List<List<string>> lines = null;
            lines = await MessageFactory.GetCsvRecords(MaximumRetryCount, RetryInterval, file, info, MyLogger);

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"get records count:{lines.Count}");
            var firstDataProp = new Dictionary<string, string>();
            if (info.FileType.Equals(FileType.Standard) && (info.SendHeaderEnabled || info.EofEnabled))
            {
                if (!lines.Any())
                {
                    var exitmsg = $"input file is empty.";
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: SendMessageAsync caused by {exitmsg}");
                    return;
                }
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

            if (info.FileType.Equals(FileType.ProductDevelopment))
            {
                var samplingIntervalStr = lines[info.SamplingIntervalLine - 1].FirstOrDefault().Trim();
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"samplingInterval: {samplingIntervalStr}");
                decimal samplingInterval = 0;
                if (decimal.TryParse(samplingIntervalStr, out samplingInterval))
                {
                    info.SamplingInterval = samplingInterval;
                }
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Processing FileType:'{info.FileType.ToString()}' lines_count:'{lines.Count}'");

            var filename = file.Name;
            var header = new List<string>();
            var recordsData = MessageFactory.GetRecordDataFromCsvData(lines, info, MyLogger);

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"send_max_records: {info.SendMaximumRecords}");
            var jsonMsg = new JsonMessage() { RecordList = new List<JsonMessage.RecordInfo>() };
            var maxRecords = info.SendMaximumRecords;
            if (!recordsData.HeaderRecords.Any()) recordsData.HeaderRecords.Add(filename);
            int cnt = 0;
            var rownum = 0;
            int sendcnt = 0;
            int rowtotal = recordsData.DataRecords.Count;
            var dataProps = new Dictionary<string, string>();
            if (info.FileType.Equals(FileType.Standard) && recordsData.HeaderRecordsStandard.Any())
            {
                foreach (var record in recordsData.HeaderRecordsStandard)
                {
                    cnt++;
                    if (rownum == 0)
                    {
                        dataProps = firstDataProp;
                        rownum = cnt;
                    }
                    var recordInfo = new JsonMessage.RecordInfo() { RecordHeader = new List<string>(), RecordData = record };
                    recordInfo.RecordHeader.AddRange(recordsData.HeaderRecords);
                    recordInfo.RecordHeader.Add(cnt.ToString());
                    jsonMsg.RecordList.Add(recordInfo);
                    if (0 < maxRecords && jsonMsg.RecordList.Count == maxRecords)
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
                        rownum = 0;
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
                var recordInfo = new JsonMessage.RecordInfo() { RecordHeader = new List<string>(), RecordData = record };
                recordInfo.RecordHeader.AddRange(recordsData.HeaderRecords);
                recordInfo.RecordHeader.Add(cnt.ToString());
                jsonMsg.RecordList.Add(recordInfo);
                if (0 < maxRecords && jsonMsg.RecordList.Count == maxRecords)
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
                    rownum = 0;
                }
            }
            if (jsonMsg.RecordList.Any())
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

        /// SearchInputFileAsyncから呼び出しのみ、Set/Unset不要
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

        /// SearchInputFileAsyncから呼び出しのみ、Set/Unset不要
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
        /// SearchInputFileAsyncから呼び出しのみ、Set/Unset不要
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
        /// MyWatcher_OnFileDetectedから呼び出しのみ、Set/Unset不要
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