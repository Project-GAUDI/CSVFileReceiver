using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TICO.GAUDI.Commons;

namespace IotedgeV2CSVFileReceiver
{
    static class MessageFactory
    {
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(MessageFactory));
        /// <summary>
        /// CSVファイルからレコードリストを生成する
        /// </summary>
        /// <param name="maxRetryCount"></param>
        /// <param name="retryInterval"></param>
        /// <param name="file"></param>
        /// <param name="info"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static async Task<List<List<string>>> GetCsvRecords(int maxRetryCount, int retryInterval, FileInfo file, TargetInfo info, ILogger logger)
        {
            logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: GetCsvRecords");

            List<List<string>> lines = null;
            for (int i = 0; i < maxRetryCount; i++)
            {
                try
                {
                    logger.WriteLog(ILogger.LogLevel.TRACE, $"GetCsvRecords:'{file.Name}'");
                    lines = new List<List<string>>();
                    using (var reader = new StreamReader(file.FullName, Encoding.GetEncoding(info.Encode)))
                    using (var csv = new CsvReader(reader, new CultureInfo(info.Culture, false)))
                    {
                        csv.Configuration.HasHeaderRecord = false;
                        csv.Configuration.Delimiter = info.Delimiter.ToString();

                        while (csv.Read())
                        {
                            var row = new List<string>();
                            for (int idx = 0; csv.TryGetField(idx, out string data); idx++)
                            {
                                // RecordDataNumで指定した列数まで取得する
                                if (info.RecordDataNum > 0 && idx == info.RecordDataNum) break;
                                row.Add(data);
                            }
                            lines.Add(row);
                        }
                    }
                    break;
                }
                catch (Exception ex) when
                (ex is DirectoryNotFoundException
                || ex is FileNotFoundException
                || ex is IOException
                || ex is System.Security.SecurityException
                || ex is UnauthorizedAccessException)
                {
                    logger.WriteLog(ILogger.LogLevel.WARN, $"Open file[{file.Name}] failed[{i + 1}/{maxRetryCount}]: {ex.Message}");
                    if ((i + 1) == maxRetryCount)
                    {
                        throw;
                    }
                    await Task.Delay(retryInterval);
                }
                catch (Exception ex)
                {
                    var errmsg = $"Open file[{file.Name}] failed.";
                    logger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg} {ex}", true);
                    logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: GetCsvRecords caused by {errmsg}");
                    throw;
                }
            }

            logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: GetCsvRecords");

            return lines;
        }

        /// <summary>
        /// CSVファイルの1行から送信するメッセージを生成
        /// </summary>
        /// <param name="line"></param>
        /// <param name="filename"></param>
        /// <param name="info"></param>
        /// <param name="rowNumber"></param>
        /// <param name="rowTotal"></param>
        /// <param name="dataProps"></param>
        /// <param name="filenameProps"></param>
        /// <returns></returns>
        public static IotMessage CreateMessage(JsonMessage jsonMessage, string filename, int rowNumber, int rowTotal, Dictionary<string, string> dataProps = null, Dictionary<string, string> filenameProps = null)
        {
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: CreateMessage");
            // Jsonメッセージのシリアライズ
            var jsonMessageByte = JsonMessage.SerializeJsonMessageByte(jsonMessage);
            var ret = new IotMessage(jsonMessageByte);

            // プロパティセット
            ret.SetProperty("filename", filename);
            ret.SetProperty("row_number", rowNumber.ToString("D"));
            ret.SetProperty("row_total", rowTotal.ToString("D"));

            if (dataProps != null && dataProps.Any())
            {
                ret.SetProperties( dataProps );
            }

            if (filenameProps != null && filenameProps.Any())
            {
                ret.SetProperties( filenameProps );
            }
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: CreateMessage");
            return ret;
        }

        /// <summary>
        /// CSVのデータをヘッダー部とデータ部に分けて取得する
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="info"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static RecordData GetRecordDataFromCsvData(List<List<string>> lines, TargetInfo info, ILogger logger)
        {
            logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: GetRecordDataFromCsvData");

            var recordData = new RecordData();
            int dataCnt = 0;
            var samplingTime = info.SamplingBaseTime;

            logger.WriteLog(ILogger.LogLevel.TRACE, $"ignore_first_row: {info.IgnoreFirstRow}");
            foreach (var line in lines.Select((data, index) => new { data, index }))
            {
                if (line.index <= info.HeaderEndLine - 1)
                {
                    if (line.index < info.HeaderStartLine - 1 || (info.FileType.Equals(FileType.Standard) && !info.SendHeaderEnabled))
                    {
                        logger.WriteLog(ILogger.LogLevel.TRACE, "skip header");
                        continue;
                    }
                    if (info.FileType.Equals(FileType.Standard))
                    {
                        recordData.HeaderRecordsStandard.Add(line.data);
                        logger.WriteLog(ILogger.LogLevel.TRACE, $"header_standard record: [{String.Join(",",line.data)}]");
                    }
                    else if (info.FileType.Equals(FileType.AAA) && info.HeaderFilter != null)
                    {
                        var createdMessageHeader = CreateMessageHeader(line.data, info.HeaderFilter, logger);
                        recordData.HeaderRecords.AddRange(createdMessageHeader);
                        logger.WriteLog(ILogger.LogLevel.TRACE, $"header record: [{String.Join(",",createdMessageHeader)}]");
                    }
                    else
                    {
                        logger.WriteLog(ILogger.LogLevel.TRACE, $"header record row {line.index}");
                        recordData.HeaderRecords.AddRange(line.data);
                        logger.WriteLog(ILogger.LogLevel.TRACE, $"header record: [{String.Join(",",line.data)}]");
                    }
                }
                else if (line.index < info.DataStartLine - 1)
                {
                    continue;
                }
                else
                {
                    // データ編集
                    if (info.DataEditOptions != null && info.DataEditOptions.Any())
                    {
                        foreach (var obj in info.DataEditOptions)
                        {
                            line.data[obj.Column - 1] = obj.GetEditValue(line.data[obj.Column - 1]);
                        }
                    }
                    // 1列目の削除
                    if (info.IgnoreFirstRow) line.data.RemoveAt(0);

                    var data = ColumnTrim(line.data);
                    if (info.FileType.Equals(FileType.AAA))
                    {
                        dataCnt++;
                        if (info.DataInterval != dataCnt)
                        {
                            continue;
                        }
                        if (info.DataConbine)
                        {
                            if (data.Last().Length == 0) data.RemoveAt(data.Count - 1);
                            if (data.Any())
                            {
                                if (recordData.DataRecords.Any())
                                {
                                    recordData.DataRecords.First().AddRange(data);
                                }
                                else
                                {
                                    recordData.DataRecords.Add(data);
                                }
                            }
                        }
                        else
                        {
                            recordData.DataRecords.Add(data);
                        }
                        dataCnt = 0;
                    }
                    else if (info.FileType.Equals(FileType.ProductDevelopment))
                    {
                        if (0 >= info.SamplingInterval)
                        {
                            if (recordData.DataRecords.Any())
                            {
                                recordData.DataRecords.First().AddRange(data);
                            }
                            else
                            {
                                recordData.DataRecords.Add(data);
                            }
                        }
                        else
                        {
                            data.Insert(0, samplingTime.ToString());
                            recordData.DataRecords.Add(data);
                            samplingTime = samplingTime + info.SamplingInterval;
                        }
                    }
                    else
                    {
                        recordData.DataRecords.Add(line.data);
                    }
                }
            }

            logger.WriteLog(ILogger.LogLevel.TRACE, $"header_standard_count: {recordData.HeaderRecordsStandard.Count} header_count: {recordData.HeaderRecords.Count} data_count: {recordData.DataRecords.Count}");
            
            logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: GetRecordDataFromCsvData");

            return recordData;
        }

        private static List<string> ColumnTrim(List<string> line)
        {
            var ret = line.Select(x => x.Trim()).ToList();
            return ret;
        }

        /// <summary>
        /// メッセージを加工する
        /// </summary>
        /// <param name="lines">ファイル読み込み結果</param>
        /// <returns></returns>
        private static List<string> CreateMessageHeader(List<string> line, Dictionary<string, string> headerFilter, ILogger logger)
        {
            logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: CreateMessageHeader");

            var csvData = new List<string>();
            // フィルタ条件によるヘッダ情報の置換
            foreach (var column in line)
            {
                var col = column.Trim();
                string val;
                if (headerFilter.TryGetValue(col, out val))
                {
                    logger.WriteLog(ILogger.LogLevel.TRACE, $"header_filter:{col} -> {val}");
                    csvData.Add(val);
                }
                else
                {
                    csvData.Add(col);
                }
            }

            logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: CreateMessageHeader");

            return csvData;
        }

        public class RecordData
        {
            public List<string> HeaderRecords = new List<string>();
            public List<List<string>> DataRecords = new List<List<string>>();
            public List<List<string>> HeaderRecordsStandard = new List<List<string>>();
        }
    }
}
