using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using TICO.GAUDI.Commons;
using YamlDotNet.Serialization;

namespace IotedgeV2CSVFileReceiver
{
    public class TargetInfo
    {
        private const string DP_DATA_PROPERTY_KEY = "data_property";
        private const string DP_DATA_EDIT_OPTION_KEY = "data_edit_option";
        private const string CULTURE_DEFAULT = "ja-JP";
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
        public int? ColumnsLine { get; private set; }
        public List<DataProperty> DataProperties { get; private set; }
        public List<DataEditOption> DataEditOptions { get; private set; }
        public long SendMaximumRecords { get; private set; }
        public double MaxMessageSize  { get; private set; }
        public long MaxFileSize { get; private set; }
        public long MessageSizeLimitBytes
        {
            get
            {
                if (MaxMessageSize == 0) return 0;
                var value = MaxMessageSize;
                if (value >= 1) return (long)(value * 1024);
                return (long)(256 * 1024 * value);
            }
        }
        public long FileSizeLimitBytes
        {
            get
            {
                if (MaxFileSize < 0) return 0;
                return checked(MaxFileSize * 1_000_000L);
            }
        }
        public long RecordDataNum { get; private set; }
        public bool FilenamePropertiesEnabled { get; private set; }
        public bool RecordMetadataEnabled { get; private set; }
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(TargetInfo));
        private TargetInfo() { }

        public static TargetInfo CreateInstance(JObject jobj, bool backupEnabled, ILogger logger = null, long defaultMaxFileSize = -1)
        {
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: CreateInstance");
            try
            {
                var ret = new TargetInfo();

                ret.OutputName = Util.GetDesiredProperty<string>(jobj, "output_name", true);
                ret.ErrorOutputName = Util.GetDesiredProperty<string>(jobj, "error_output_name", false, null);
                ret.Filter = Util.GetDesiredProperty<string>(jobj, "filter", true);
                ret.Encode = Util.GetDesiredProperty<string>(jobj, "encode", true, condition: (value) => value.ToLower().Equals("utf-8") || value.ToLower().Equals("shift-jis"));
                ret.Delimiter = Util.GetDesiredProperty<string>(jobj, "delimiter", false, ",", condition: (value) => !string.IsNullOrEmpty(value) && value.Length == 1)[0];
                ret.DataStartLine = Util.GetDesiredProperty<int>(jobj, "data_start_line", true, condition: (value) => value >= 1);
                ret.EofEnabled = Util.GetDesiredProperty<bool>(jobj, "eof_enabled", false, false);
                ret.AfterProcess = Util.GetDesiredProperty<AfterProcess>(jobj, "after_process", true);

                if (!backupEnabled && ret.AfterProcess == AfterProcess.Move)
                {
                    var errmsg = $"after_process can't set {ret.AfterProcess} when BackupPath is not set or does not exist.";
                    _logger.WriteLog(ILogger.LogLevel.ERROR, errmsg);
                    throw new Exception(errmsg);
                }

                ret.DataProperties = new List<DataProperty>();

                ret.SendHeaderEnabled = Util.GetDesiredProperty<bool>(jobj, "send_header_enabled", false, false);
                int count = 1;
                while (true)
                {
                    var val = Util.GetDesiredProperty<JObject>(jobj, DP_DATA_PROPERTY_KEY + count.ToString("D"), false, null);
                    if (val == null) break;
                    var name = Util.GetDesiredProperty<string>(val, "name", true);
                    var col = Util.GetDesiredProperty<int>(val, "column", true, condition: (value) => value >= 1);
                    var get_from = Util.GetDesiredProperty<GetFrom>(val, "get_from", true);
                    ret.DataProperties.Add(new DataProperty(name, col , get_from));
                    count++;
                }

                if (ret.SendHeaderEnabled)
                {
                    ret.HeaderStartLine = Util.GetDesiredProperty<int>(jobj, "header_start_line", true, condition: (value) => value >= 1);
                    ret.HeaderEndLine = Util.GetDesiredProperty<int>(jobj, "header_end_line", false, ret.HeaderStartLine, condition: (value) => value >= ret.HeaderStartLine && value < ret.DataStartLine);
                }
                if (jobj.ContainsKey("columns_line"))
                {
                    ret.ColumnsLine = Util.GetDesiredProperty<int>(jobj, "columns_line", true, condition: (value) => value >= 1 && value < ret.DataStartLine);
                }
                ret.Culture = Util.GetDesiredProperty<string>(jobj, "culture", false, CULTURE_DEFAULT);
                ret.SendMaximumRecords = Util.GetDesiredProperty<int>(jobj, "send_max_records", false, 0);
                ret.MaxMessageSize = Util.GetDesiredProperty<double>(jobj, "max_message_size", false, 0.7, condition: (value) => value == 0 || (value >= 0.01 && value <= 0.99) || (value >= 1 && value <= 256 && value == Math.Floor(value)));  
                if (jobj.ContainsKey("max_file_size"))
                {
                    var maxFileSize = Util.GetDesiredProperty<double>(jobj, "max_file_size", true, condition: (value) => value == -1 || (value >= 1 && value == Math.Floor(value) && value <= long.MaxValue / 1_000_000L));
                    ret.MaxFileSize = (long)maxFileSize;
                }
                else
                {
                    ret.MaxFileSize = defaultMaxFileSize;
                }
                ret.RecordDataNum = Util.GetDesiredProperty<int>(jobj, "record_data_num", false, 0);
                ret.FilenamePropertiesEnabled = Util.GetDesiredProperty<bool>(jobj, "filename_properties_enabled", false, false);
                ret.RecordMetadataEnabled = Util.GetDesiredProperty<bool>(jobj, "record_metadata_enabled", false, false);

                ret.DataEditOptions = new List<DataEditOption>();
                for (int i = 1; jobj.TryGetValue(DP_DATA_EDIT_OPTION_KEY + i.ToString("D"), out JToken token); i++)
                {
                    var val = (JObject)token;
                    ret.DataEditOptions.Add(DataEditOption.CreateInstance(val));
                }
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: CreateInstance");
                return ret;
            }catch(Exception ex)
            {
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: CreateInstance caused by {ex.Message}");
                throw new Exception($"Failed to create instance: {ex.Message}");
            }

        }

        public void OutputSettingValues(ILogger logger, int no = 0)
        {
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: OutputSettingValues");
            if (no > 0) logger.WriteLog(ILogger.LogLevel.INFO, $"TargetInfo{no}");

            try
            {
                string serialized = new Serializer().Serialize(this);
                List<string> outList = serialized.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
                foreach (string output in outList)
                {
                    logger.WriteLog(ILogger.LogLevel.INFO, $"  {output}");
                }
            }
            catch
            {
                var errmsg = $"Failed to OutputSettingValues.";
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: OutputSettingValues caused by {errmsg}");
                throw new Exception(errmsg);
            }  
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: OutputSettingValues");
        }
    }
}
