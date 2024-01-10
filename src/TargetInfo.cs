using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using TICO.GAUDI.Commons;
using YamlDotNet.Serialization;

namespace CSVFileReceiver
{
    public class TargetInfo
    {
        private const string DP_DATA_PROPERTY_KEY = "data_property";
        private const string DP_DATA_EDIT_OPTION_KEY = "data_edit_option";
        private const string CULTURE_DEFAULT = "ja-JP";
        public FileType FileType { get; private set; }
        public string OutputName { get; private set; }
        public string ErrorOutputName { get; private set; }
        public string Filter { get; private set; }
        public string Encode { get; private set; }
        public string Culture { get; private set; }
        public char Delimiter { get; private set; }
        public int DataStartLine { get; private set; }
        public bool EofEnabled { get; private set; }
        public AfterProcess AfterProcess { get; private set; }
        public bool SendHeaderEnabled { get; private set; }
        public int HeaderStartLine { get; private set; }
        public int HeaderEndLine { get; private set; }
        public List<DataProperty> DataProperties { get; private set; }
        public List<DataEditOption> DataEditOptions { get; private set; }
        public Dictionary<string, string> HeaderFilter { get; private set; }
        public long DataInterval { get; private set; } = 1;
        public bool IgnoreFirstRow { get; private set; } = false;
        public bool DataConbine { get; private set; } = false;
        public int SamplingIntervalLine { get; private set; }
        public decimal SamplingInterval { get; set; }
        public decimal SamplingBaseTime { get; private set; } = 0.0m;
        public string TimestampFormat { get; private set; }
        public long SendMaximumRecords { get; private set; }
        public long RecordDataNum { get; private set; }
        public bool FilenamePropertiesEnabled { get; private set; }
        private TargetInfo() { }

        public static TargetInfo CreateInstance(JObject jobj, bool backupEnabled, Logger logger = null)
        {
            var ret = new TargetInfo();
            ret.FileType = Util.GetRequiredValue<string>(jobj, "file_type").ToFileType();
            ret.OutputName = Util.GetRequiredValue<string>(jobj, "output_name");
            try
            {
                ret.ErrorOutputName = Util.GetRequiredValue<string>(jobj, "error_output_name");
            }
            catch (Exception)
            {
                ret.ErrorOutputName = "";
            }
            ret.Filter = Util.GetRequiredValue<string>(jobj, "filter");
            ret.Encode = Util.GetRequiredValue<string>(jobj, "encode");
            ret.Delimiter = Util.GetRequiredValue<string>(jobj, "delimiter")[0];
            ret.DataStartLine = Util.GetRequiredValue<int>(jobj, "data_start_line");
            ret.EofEnabled = Util.GetRequiredValue<bool>(jobj, "eof_enabled");
            ret.AfterProcess = Util.GetRequiredValue<string>(jobj, "after_process").ToAfterProcess();

            // Standard:AAA:PDをサポートする
            if (!ret.FileType.IsExists())
            {
                throw new Exception($"{ret.FileType} is not supported.");
            }

            if (ret.DataStartLine < 1)
            {
                throw new Exception($"data_start_line can't set {ret.DataStartLine}");
            }

            if(!backupEnabled && ret.AfterProcess == AfterProcess.Move)
            {
                throw new Exception($"after_process can't set {ret.AfterProcess} when BackupPath is not set.");
            }

            ret.DataProperties = new List<DataProperty>();
            if (ret.FileType.Equals(FileType.Standard))
            {
                ret.SendHeaderEnabled = Util.GetRequiredValue<bool>(jobj, "send_header_enabled");

                for (int i = 1; jobj.TryGetValue(DP_DATA_PROPERTY_KEY + i.ToString("D"), out JToken token); i++)
                {
                    var val = (JObject)token;
                    var name = Util.GetRequiredValue<string>(val, "name");
                    var col = Util.GetRequiredValue<int>(val, "column");
                    var get_from = GetFrom.Message;
                    if (val.TryGetValue("get_from", out JToken dummy_token)){
                        get_from = Util.GetRequiredValue<string>(val, "get_from").ToGetFrom();
                    }
                    
                    if (col < 1)
                    {
                        throw new Exception($"column can't set {col}");
                    }
                    ret.DataProperties.Add(new DataProperty(name, col , get_from));
                }
            }

            if (ret.SendHeaderEnabled || ret.FileType.Equals(FileType.AAA) || ret.FileType.Equals(FileType.ProductDevelopment))
            {
                ret.HeaderStartLine = Util.GetRequiredValue<int>(jobj, "header_start_line");
                ret.HeaderEndLine = Util.GetRequiredValue<int>(jobj, "header_end_line");
                if(ret.DataStartLine <= ret.HeaderEndLine)
                {
                    throw new Exception($"header_end_line can't set {ret.HeaderEndLine} greater than or equal data_start_line {ret.DataStartLine}.");
                }
                if(ret.HeaderEndLine < ret.HeaderStartLine)
                {
                    throw new Exception($"header_start_line can't set {ret.HeaderStartLine} greater than header_end_line {ret.HeaderEndLine}.");
                }
            }

            if (ret.FileType.Equals(FileType.AAA))
            {
                if (jobj.TryGetValue("header_filter", out JToken headerFilter))
                {
                    // 動作確認できず。恐らくバグで、実装後テストされていない？
                    ret.HeaderFilter = (Dictionary<string, string>)((JValue)headerFilter).Value;
                }
                if (jobj.TryGetValue("ignore_first_row", out JToken ignoreFirstRow))
                {
                    ret.IgnoreFirstRow = (bool)((JValue)ignoreFirstRow).Value;
                }
                if (jobj.TryGetValue("data_interval", out JToken dataInterval))
                {
                    ret.DataInterval = (long)((JValue)dataInterval).Value;
                }
                if (jobj.TryGetValue("data_conbine", out JToken dataConbine))
                {
                    ret.DataConbine = (bool)((JValue)dataConbine).Value;
                }
            }
            else if (ret.FileType.Equals(FileType.ProductDevelopment))
            {
                ret.SamplingIntervalLine = Util.GetRequiredValue<int>(jobj, "sampling_interval_line");
                if (jobj.TryGetValue("sampling_base_time", out JToken samplingBaseTime))
                {
                    try
                    {
                        ret.SamplingBaseTime = (decimal)((JValue)samplingBaseTime).Value;
                    }
                    catch (Exception)
                    {
                        ret.SamplingBaseTime = 0.0m;
                    }
                }
                ret.TimestampFormat = Util.GetRequiredValue<string>(jobj, "timestamp_format");
            }

            if (jobj.TryGetValue("culture", out JToken token2))
            {
                ret.Culture = (string)((JValue)token2).Value;
            }
            else
            {
                ret.Culture = CULTURE_DEFAULT;
            }

            if (jobj.TryGetValue("send_max_records", out JToken recordMaxNum))
            {
                var maxRecords = (long)((JValue)recordMaxNum).Value;
                if (0 < maxRecords) ret.SendMaximumRecords = maxRecords;
                else ret.SendMaximumRecords = 0;
            }
            else
            {
                ret.SendMaximumRecords = 0;
            }

            if (jobj.TryGetValue("record_data_num", out JToken recordDataNum))
            {
                var dataNum = (long)((JValue)recordDataNum).Value;
                if (0 < dataNum) ret.RecordDataNum = dataNum;
                else ret.RecordDataNum = 0;
            }
            else
            {
                ret.RecordDataNum = 0;
            }

            if (jobj.TryGetValue("filename_properties_enabled", out JToken filenamePropertiesEnabled))
            {
                ret.FilenamePropertiesEnabled = (bool)((JValue)filenamePropertiesEnabled).Value;
            }
            else
            {
                ret.FilenamePropertiesEnabled  = false;
            }

            ret.DataEditOptions = new List<DataEditOption>();
            for (int i = 1; jobj.TryGetValue(DP_DATA_EDIT_OPTION_KEY + i.ToString("D"), out JToken token); i++)
            {
                var val = (JObject)token;
                ret.DataEditOptions.Add(DataEditOption.CreateInstance(val));
            }
            
            return ret;
        }

        public void OutputSettingValues(Logger logger, int no = 0)
        {
            if (no > 0) logger.WriteLog(Logger.LogLevel.INFO, $"TargetInfo{no}");

            try
            {
                string serialized = new Serializer().Serialize(this);
                List<string> outList = serialized.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
                foreach (string output in outList)
                {
                    logger.WriteLog(Logger.LogLevel.INFO, $"  {output}");
                }
            }
            catch
            {
                throw new Exception("Failed to OutputSettingValues.");
            }  

        }
    }
}
