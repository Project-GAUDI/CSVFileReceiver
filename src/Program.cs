namespace CSVFileReceiver
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using TICO.GAUDI.Commons;

    class Program
    {
        const string DP_INFO_KEY = "info";

        static IModuleClient MyModuleClient { get; set; } = null;

        static Logger MyLogger { get; } = Logger.GetLogger(typeof(Program));

        static bool IsReady { get; set; } = false;

        static string InputPath { get; set; } = null;

        static string BackupPath { get; set; } = null;

        static string ErrorPath { get; set; } = null;

        static string SortKey { get; set; } = null;

        static string SortOrder { get; set; } = null;

        static int MaximumRetryCount { get; set; } = 5;

        static int RetryInterval { get; set; } = 5000;

        static int WaitTime { get; set; } = 0;

        static List<TargetInfo> TargetInfos { get; set; }

        static FileWatcher MyWatcher { get; set; }

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            try
            {
                Init().Wait();
            }
            catch (Exception e)
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"Init failed. {e}", true);
                Environment.Exit(1);
            }

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");

            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// </summary>
        static async Task Init()
        {
            // 取得済みのModuleClientを解放する
            if (MyModuleClient != null)
            {
                await MyModuleClient.CloseAsync();
                MyModuleClient.Dispose();
                MyModuleClient = null;
            }

            // 環境変数から送信トピックを判定
            TransportTopic defaultSendTopic = TransportTopic.Iothub;
            string sendTopicEnv = Environment.GetEnvironmentVariable("DefaultSendTopic");
            if (Enum.TryParse(sendTopicEnv, true, out TransportTopic sendTopic))
            {
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"DefaultSendTopic\" is {sendTopicEnv}.");
                defaultSendTopic = sendTopic;
            }
            else
            {
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"DefaultSendTopic\" is not set. Default value ({defaultSendTopic}) assigned.");
            }

            // 環境変数から受信トピックを判定
            TransportTopic defaultReceiveTopic = TransportTopic.Iothub;
            string receiveTopicEnv = Environment.GetEnvironmentVariable("DefaultReceiveTopic");
            if (Enum.TryParse(receiveTopicEnv, true, out TransportTopic receiveTopic))
            {
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"DefaultReceiveTopic\" is {receiveTopicEnv}.");
                defaultReceiveTopic = receiveTopic;
            }
            else
            {
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"DefaultReceiveTopic\" is not set. Default value ({defaultReceiveTopic}) assigned.");
            }

            // MqttModuleClientを作成
            if (Boolean.TryParse(Environment.GetEnvironmentVariable("M2MqttFlag"), out bool m2mqttFlag) && m2mqttFlag)
            {
                string sasTokenEnv = Environment.GetEnvironmentVariable("SasToken");
                MyModuleClient = new MqttModuleClient(sasTokenEnv, defaultSendTopic: defaultSendTopic, defaultReceiveTopic: defaultReceiveTopic);
            }
            // IoTHubModuleClientを作成
            else
            {
                ITransportSettings[] settings = null;
                string protocolEnv = Environment.GetEnvironmentVariable("TransportProtocol");
                if (Enum.TryParse(protocolEnv, true, out TransportProtocol transportProtocol))
                {
                    MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"TransportProtocol\" is {protocolEnv}.");
                    settings = transportProtocol.GetTransportSettings();
                }
                else
                {
                    // settings = null の場合、CreateAsyncにTransportProtocol.Amqpの設定が適応される
                    MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"TransportProtocol\" is not set. Default value ({TransportProtocol.Amqp}) assigned.");
                }

                MyModuleClient = await IotHubModuleClient.CreateAsync(settings, defaultSendTopic, defaultReceiveTopic).ConfigureAwait(false);
            }

            // edgeHubへの接続
            while (true)
            {
                try
                {
                    await MyModuleClient.OpenAsync().ConfigureAwait(false);
                    break;
                }
                catch (Exception e)
                {
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Open a connection to the Edge runtime is failed. {e.Message}");
                    await Task.Delay(1000);
                }
            }

            // Loggerへモジュールクライアントを設定
            Logger.SetModuleClient(MyModuleClient);

            // 環境変数からログレベルを設定
            string logEnv = Environment.GetEnvironmentVariable("LogLevel");
            try
            {
                if (logEnv != null) Logger.SetOutputLogLevel(logEnv);
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"Output log level is: {Logger.OutputLogLevel.ToString()}");
            }
            catch (ArgumentException e)
            {
                MyLogger.WriteLog(Logger.LogLevel.WARN, $"Environment LogLevel does not expected string. Exception:{e.Message}");
            }

            // モジュール固有の環境変数
            var existDir = true;
            InputPath = Environment.GetEnvironmentVariable("InputPath");
            if (string.IsNullOrEmpty(InputPath))
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, "Environment InputPath does not set.", true);
                existDir = false;
            }
            else if (!Directory.Exists(InputPath))
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"Environment InputPath[{InputPath}] does not exist.", true);
                existDir = false;
            }
            MyLogger.WriteLog(Logger.LogLevel.INFO, $"InputPath = {InputPath}");

            var backupEnabled = false;
            BackupPath = Environment.GetEnvironmentVariable("BackupPath");
            if (string.IsNullOrEmpty(BackupPath))
            {
                MyLogger.WriteLog(Logger.LogLevel.INFO, "Environment BackupPath does not set. File back up is disabled.");
            }
            else if (Directory.Exists(BackupPath))
            {
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"BackupPath = {BackupPath}");
                backupEnabled = true;
            }
            else
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"Environment BackupPath[{BackupPath}] does not exist.", true);
                existDir = false;
            }

            ErrorPath = Environment.GetEnvironmentVariable("ErrorPath");
            if (string.IsNullOrEmpty(ErrorPath))
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, "Environment ErrorPath does not set.", true);
                existDir = false;
            }
            else if (!Directory.Exists(ErrorPath))
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"Environment ErrorPath[{ErrorPath}] does not exist.", true);
                existDir = false;
            }
            MyLogger.WriteLog(Logger.LogLevel.INFO, $"ErrorPath = {ErrorPath}");

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
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Environment MaximumRetryCount can't set {maxCon}. Default value ({MaximumRetryCount}) assigned.");
                }
            }
            MyLogger.WriteLog(Logger.LogLevel.INFO, $"MaximumRetryCount = {MaximumRetryCount}");

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
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Environment RetryInterval can't set {interval}. Default value ({RetryInterval}) assigned.");
                }
            }
            MyLogger.WriteLog(Logger.LogLevel.INFO, $"RetryInterval = {RetryInterval}");

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
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Environment WaitTime can't set {waittime}. Default value ({WaitTime}) assigned.");
                }
            }
            MyLogger.WriteLog(Logger.LogLevel.INFO, $"WaitTime = {WaitTime}");

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
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Environment SortKey can't set {key}. Default value ({SortKey}) assigned.");
                }
            }
            MyLogger.WriteLog(Logger.LogLevel.INFO, $"SortKey = {SortKey}");

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
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Environment SortOrder can't set {order}. Default value ({SortOrder}) assigned.");
                }
            }
            MyLogger.WriteLog(Logger.LogLevel.INFO, $"SortOrder = {SortOrder}");

            IsReady = false;
            if (existDir)
            {
                // desiredプロパティの取得
                var twin = await MyModuleClient.GetTwinAsync().ConfigureAwait(false);
                var collection = twin.Properties.Desired;
                IsReady = SetMyProperties(collection, backupEnabled);
            }
            // プロパティ更新時のコールバックを登録
            await MyModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null).ConfigureAwait(false);

            if (IsReady)
            {
                MyWatcher = new FileWatcher(InputPath, SortKey, SortOrder);
                MyWatcher.OnFileDetected += MyWatcher_OnFileDetected;
                MyWatcher.Start();
            }
        }

        private static async Task MyWatcher_OnFileDetected(object sender, FileDtectedEventArgs e)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: MyWatcher_OnFileDetected");
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"TargetInfos count {TargetInfos.Count}");

            var file = e.InputFile;
            var cnt = 0;
            foreach (var info in TargetInfos)
            {
                cnt++;
                MyLogger.WriteLog(Logger.LogLevel.TRACE, $"TargetInfo No{cnt}");
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

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: MyWatcher_OnFileDetected");
        }

        static async Task SendMessageAsync(FileInfo file, TargetInfo info)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: SendMessageAsync");

            var filenameProps = new Dictionary<string, string>();
            if(info.FilenamePropertiesEnabled)
            {
                string pattern = "[0-9a-zA-Z]{3}_(?<format>[0-9a-zA-Z]{3})_(?<country>[0-9a-zA-Z]{2})(?<company>[0-9a-zA-Z]{2})(?<factory>[0-9a-zA-Z]{3})(?<data_type>[0-9a-zA-Z]{3})(?<free_area>[0-9a-zA-Z]{5})_.+\\.(C|c)(S|s)(V|v)";
                Regex reg = new Regex(pattern);
                Match m = reg.Match(file.Name);
                if (m.Success)
                {
                    filenameProps.Add("country",m.Groups["country"].Value);
                    filenameProps.Add("company",m.Groups["company"].Value);
                    filenameProps.Add("factory",m.Groups["factory"].Value);
                    filenameProps.Add("data_type",m.Groups["data_type"].Value);
                    filenameProps.Add("free_area",m.Groups["free_area"].Value);
                    filenameProps.Add("format",m.Groups["format"].Value);
                }
                else
                {
                    throw new Exception($"ファイル名({file.Name})がGAUDI標準のCSVファイル命名規約「送信先No.(3桁)_フォーマットNo.(3桁)_データ取得場所(15桁)_可変データ(N桁).csv」に従っていません");
                }
            }

            List<List<string>> lines = null;
            lines = await MessageFactory.GetCsvRecords(MaximumRetryCount, RetryInterval, file, info, MyLogger);

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"get records count:{lines.Count}");
            var firstDataProp = new Dictionary<string, string>();
            if (info.FileType.Equals(FileType.Standard) && (info.SendHeaderEnabled || info.EofEnabled))
            {
                if (!lines.Any())
                {
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Exit Method: SendMessageAsync caused by input file is empty.");
                    return;
                }
            }

            int startIndex = info.DataStartLine - 1;
            if ((0 <= startIndex) && ( startIndex <= lines.Count - 1))
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
                MyLogger.WriteLog(Logger.LogLevel.TRACE, $"samplingInterval: {samplingIntervalStr}");
                decimal samplingInterval = 0;
                if (decimal.TryParse(samplingIntervalStr, out samplingInterval))
                {
                    info.SamplingInterval = samplingInterval;
                }
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Processing FileType:'{info.FileType.ToString()}' lines_count:'{lines.Count}'");

            var filename = file.Name;
            var header = new List<string>();
            var recordsData = MessageFactory.GetRecordDataFromCsvData(lines, info, MyLogger);

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"send_max_records: {info.SendMaximumRecords}");
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
                        using (var msg = MessageFactory.CreateMessage(jsonMsg, filename, rownum, rowtotal, dataProps,filenameProps))
                        {
                            var msgSize = msg.GetBodyStream().Length;
                            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"message size:{msgSize}");
                            await MyModuleClient.SendEventAsync(info.OutputName, msg);
                            await WaitAfterSendAsync();
                            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"send {jsonMsg.RecordList.Count} records.");
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
                        if(dataProperty.Get_From == GetFrom.File){
                            dataProps.Add(dataProperty.Name,firstDataProp[dataProperty.Name]);
                        }else{
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
                    using (var msg = MessageFactory.CreateMessage(jsonMsg, filename, rownum, rowtotal, dataProps,filenameProps))
                    {
                        var msgSize = msg.GetBodyStream().Length;
                        MyLogger.WriteLog(Logger.LogLevel.TRACE, $"message size:{msgSize}");
                        await MyModuleClient.SendEventAsync(info.OutputName, msg);
                        await WaitAfterSendAsync();
                        MyLogger.WriteLog(Logger.LogLevel.TRACE, $"send {jsonMsg.RecordList.Count} records.");
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
                using (var msg = MessageFactory.CreateMessage(jsonMsg, filename, rownum, rowtotal, dataProps,filenameProps))
                {
                    var msgSize = msg.GetBodyStream().Length;
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"message size:{msgSize}");
                    await MyModuleClient.SendEventAsync(info.OutputName, msg);
                    await WaitAfterSendAsync();
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"send {jsonMsg.RecordList.Count} records.");
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

                    await MyModuleClient.SendEventAsync(info.OutputName, eof);
                    await WaitAfterSendAsync();
                    sendcnt++;
                    if ((int)Logger.OutputLogLevel <= (int)Logger.LogLevel.TRACE)
                        MyLogger.WriteLog(Logger.LogLevel.INFO, $"Send EOF Message of {file.Name}.");
                }
            }

            MyLogger.WriteLog(Logger.LogLevel.INFO, $"Message send completed. '{file.Name}', {sendcnt} messages, {cnt} records, '{info.OutputName}'");

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: SendMessageAsync");
        }

        static async Task DeleteFileAsync(FileInfo file)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: DeleteFileAsync");

            // if ((int)Logger.OutputLogLevel <= (int)Logger.LogLevel.TRACE)
            //     MyLogger.WriteLog(Logger.LogLevel.TRACE, $"DeleteFileAsync called.  file={file.Name}");

            for (int i = 0; i < MaximumRetryCount; i++)
            {
                try
                {
                    file.Delete();
                    break;
                }
                catch (Exception exp) when
                (exp is IOException
                || exp is System.Security.SecurityException
                || exp is UnauthorizedAccessException)
                {
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Delete file[{file.Name}] failed[{i + 1}/{MaximumRetryCount}]: {exp.Message}");
                    if ((i + 1) == MaximumRetryCount)
                    {
                        throw;
                    }
                    await Task.Delay(RetryInterval);
                }
                catch (Exception exp)
                {
                    MyLogger.WriteLog(Logger.LogLevel.ERROR, $"Delete file[{file.Name}] failed: {exp.Message}");
                    throw;
                }
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: DeleteFileAsync");
        }

        static async Task MoveFileAsync(FileInfo file, string DestPath)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: MoveFileAsync");

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
                catch (Exception exp) when
                (exp is DirectoryNotFoundException
                || exp is FileNotFoundException
                || exp is IOException
                || exp is System.Security.SecurityException
                || exp is UnauthorizedAccessException)
                {
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Move file[{file.FullName}] to [{path}] failed[{i + 1}/{MaximumRetryCount}]: {exp.Message}");
                    if ((i + 1) == MaximumRetryCount)
                    {
                        throw;
                    }
                    await Task.Delay(RetryInterval);
                }
                catch (Exception exp)
                {
                    MyLogger.WriteLog(Logger.LogLevel.ERROR, $"Move file[{file.Name}] to [{path}] failed: {exp.Message}");
                    throw;
                }
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: MoveFileAsync");
        }

        /// <summary>
        /// プロパティ更新時のコールバック処理
        /// </summary>
        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: OnDesiredPropertiesUpdate");
            MyLogger.WriteLog(Logger.LogLevel.INFO, "Updating desired properties.");

            try
            {
                if (MyWatcher != null)
                {
                    await MyWatcher.Stop();
                    MyWatcher.Dispose();
                    MyWatcher = null;
                }
                await Init();
            }
            catch (Exception e)
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"OnDesiredPropertiesUpdate failed. {e}", true);
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: OnDesiredPropertiesUpdate");
        }

        /// <summary>
        /// desiredプロパティから自クラスのプロパティをセットする
        /// </summary>
        /// <returns>desiredプロパティに想定しない値があればfalseを返す</returns>
        static bool SetMyProperties(TwinCollection desiredProperties, bool backupEnabled)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");

            MyLogger.WriteLog(Logger.LogLevel.INFO, "Reading desired properties.");
            TargetInfos = new List<TargetInfo>();
            for (int i = 1; desiredProperties.Contains(DP_INFO_KEY + i.ToString("D")); i++)
            {
                JObject jobj = desiredProperties[DP_INFO_KEY + i.ToString("D")];
                try
                {
                    var target = TargetInfo.CreateInstance(jobj, backupEnabled, MyLogger);
                    TargetInfos.Add(target);
                    if ((int)Logger.OutputLogLevel <= (int)Logger.LogLevel.INFO) target.OutputSettingValues(MyLogger, i);
                }
                catch (Exception e)
                {
                    MyLogger.WriteLog(Logger.LogLevel.ERROR, $"SetMyProperties failed. {e}", true);
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");
                    return false;
                }
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: {System.Reflection.MethodBase.GetCurrentMethod().Name}");
            return true;
        }

        /// <summary>
        /// エラー発生時のメッセージ送信処理（空のメッセージを送信する）
        /// エラーファイル名は
        /// </summary>
        /// <param name="errOutputName"></param>
        /// <returns></returns>
        private static async Task ErrorSendEventAsync(string errOutputName, string fileName)
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: ErrorSendEventAsync");

            try
            {
                if (string.IsNullOrEmpty(errOutputName))
                {
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Exit Method: ErrorSendEventAsync caused by errOutputName is null or empty.");
                    return;
                }
                var msg = new IotMessage("{}");
                msg.SetProperty("filename", fileName);
                await MyModuleClient.SendEventAsync(errOutputName, msg);
                await WaitAfterSendAsync();
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"ErrorSendEventAsync : sent completed. output_name='{errOutputName}'");
            }
            catch (Exception e)
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"ErrorSendEventAsync failed. {e}", true);
            }

            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: ErrorSendEventAsync");
        }

        /// <summary>
        /// WaitTimeで指定した時間待機する
        /// </summary>
        /// <returns></returns>
        private static async Task WaitAfterSendAsync()
        {
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"Start Method: WaitAfterSendAsync");

            if( 0 < WaitTime ){
                await System.Threading.Tasks.Task.Delay(WaitTime);
            }
            
            MyLogger.WriteLog(Logger.LogLevel.TRACE, $"End Method: WaitAfterSendAsync");
        }
    }
}
