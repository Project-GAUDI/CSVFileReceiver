using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TICO.GAUDI.Commons;

namespace IotedgeV2CSVFileReceiver
{
    public abstract class DataEditOption
    {
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(DataEditOption));
        public int Column { get; private set; }

        public string Mode
        {
            get
            {
                return this.GetType().ToString();
            }
        }

        public static DataEditOption CreateInstance(JObject jobj)
        {
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: CreateInstance");
            int column = Util.GetRequiredValue<int>(jobj, "column");
            if (column < 1)
            {
                var errmsg = $"column can't set {column}";
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: CreateInstance caused by {errmsg}");
                throw new Exception(errmsg);
            }

            string mode = Util.GetRequiredValue<string>(jobj, "mode");
            if (string.IsNullOrEmpty(mode))
            {
                var errmsg = $"Property 'mode' dose not exist.";
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: CreateInstance caused by {errmsg}");
                throw new Exception(errmsg);
            }

            mode = mode.ToLower();

            DataEditOption ret;
            if (mode.Equals("round"))
            {
                ret = new DataEditOptionRound(jobj);
            }
            else if (mode.Equals("floor"))
            {
                ret = new DataEditOptionFloor(jobj);
            }
            else if (mode.Equals("truncate"))
            {
                ret = new DataEditOptionTruncate(jobj);
            }
            else if (mode.Equals("ceiling"))
            {
                ret = new DataEditOptionCeiling(jobj);
            }
            else if (mode.Equals("substring"))
            {
                ret = new DataEditOptionSubstring(jobj);
            }
            else
            {
                var errmsg = $"mode:{mode} is not supported.";
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: CreateInstance caused by {errmsg}");
                throw new Exception(errmsg);
            }

            ret.Column = column;
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: CreateInstance");
            return ret;
        }

        public abstract string GetEditValue(string input);
    }

    abstract class DataEditOptionFloatRounding : DataEditOption
    {
        // 元protected。シリアライズするためにpublicに変更
        public int Digits { get; private set; }
        // 元protected。シリアライズするためにpublicに変更
        public string OutputFormat { get; private set; }
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(DataEditOptionFloatRounding));

        public DataEditOptionFloatRounding(JObject jobj)
        {
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: DataEditOptionFloatRounding");
            Digits = Util.GetRequiredValue<int>(jobj, "digits");
            if (Digits < 0)
            {
                var errmsg = $"digits can't set {Digits}";
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: DataEditOptionFloatRounding caused by {errmsg}");
                throw new Exception(errmsg);
            }

            OutputFormat = Util.GetRequiredValue<string>(jobj, "output_format");
            if (string.IsNullOrEmpty(OutputFormat))
            {
                var errmsg = $"Property 'output_format' dose not exist.";
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: DataEditOptionFloatRounding caused by {errmsg}");
                throw new Exception(errmsg);
            }
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: DataEditOptionFloatRounding");
        }
    }

    class DataEditOptionRound : DataEditOptionFloatRounding
    {
        public DataEditOptionRound(JObject jobj) : base(jobj) { }
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(DataEditOptionRound));

        public override string GetEditValue(string input)
        {
            decimal d1 = decimal.Parse(input, System.Globalization.NumberStyles.Float);
            decimal d2 = Math.Round(d1, Digits, MidpointRounding.AwayFromZero);
            return d2.ToString(OutputFormat);
        }
    }

    class DataEditOptionFloor : DataEditOptionFloatRounding
    {
        public DataEditOptionFloor(JObject jobj) : base(jobj) { }
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(DataEditOptionFloor));

        public override string GetEditValue(string input)
        {
            decimal d1 = decimal.Parse(input, System.Globalization.NumberStyles.Float);
            var pow = Convert.ToDecimal(Math.Pow(10, Digits));
            decimal d2 = Math.Floor(d1 * pow) / pow;
            return d2.ToString(OutputFormat);
        }
    }

    class DataEditOptionTruncate : DataEditOptionFloatRounding
    {
        public DataEditOptionTruncate(JObject jobj) : base(jobj) { }
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(DataEditOptionTruncate));

        public override string GetEditValue(string input)
        {
            decimal d1 = decimal.Parse(input, System.Globalization.NumberStyles.Float);
            var pow = Convert.ToDecimal(Math.Pow(10, Digits));
            decimal d2 = Math.Truncate(d1 * pow) / pow;
            return d2.ToString(OutputFormat);
        }
    }

    class DataEditOptionCeiling : DataEditOptionFloatRounding
    {
        public DataEditOptionCeiling(JObject jobj) : base(jobj) { }
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(DataEditOptionCeiling));

        public override string GetEditValue(string input)
        {
            decimal d1 = decimal.Parse(input, System.Globalization.NumberStyles.Float);
            var pow = Convert.ToDecimal(Math.Pow(10, Digits));
            decimal d2 = Math.Ceiling(d1 * pow) / pow;
            return d2.ToString(OutputFormat);
        }
    }

    class DataEditOptionSubstring : DataEditOption
    {
        // 元private。シリアライズするためにpublicに変更
        public int Start_index { get; set; } = -1;
        // 元private。シリアライズするためにpublicに変更
        public int Length { get; set; } = -1;
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(DataEditOptionSubstring));

        public DataEditOptionSubstring(JObject jobj)
        {
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: DataEditOptionSubstring");
            Start_index = Util.GetRequiredValue<int>(jobj, "startindex");
            if (Start_index < 0)
            {
                var errmsg = $"startindex can't set {Start_index}";
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: DataEditOptionSubstring caused by {errmsg}");
                throw new Exception(errmsg);
            }

            if (jobj.TryGetValue("length", out JToken token1))
            {
                string tmpstr = ((JValue)token1).Value.ToString();
                if (int.TryParse(tmpstr, out int val))
                {
                    if (val > 0)
                    {
                        Length = val;
                    }
                }
            }
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: DataEditOptionSubstring");
        }

        public override string GetEditValue(string input)
        {
            string ret = null;
            if (string.IsNullOrEmpty(input))
            {
                ret = "";
            }
            else
            {
                ret = input;
                if (ret.Length >= this.Start_index)
                {
                    if (this.Length > 0)
                    {
                        if (ret.Length >= this.Start_index + this.Length)
                        {
                            ret = ret.Substring(this.Start_index, this.Length);
                        }
                        else
                        {
                            ret = ret.Substring(this.Start_index);
                        }
                    }
                    else
                    {
                        ret = ret.Substring(this.Start_index);
                    }
                }
            }
            return ret;
        }
    }
}
