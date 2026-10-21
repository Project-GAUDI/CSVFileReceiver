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

        public static DataEditOption CreateInstance(JObject jobj)
        {
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: CreateInstance");
            try
            {
                int column = Util.GetDesiredProperty<int>(jobj, "column", true, condition: (value) => value >= 1);
                string mode = Util.GetDesiredProperty<string>(jobj, "mode", true);
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
            catch (Exception ex)
            {
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: CreateInstance caused by {ex.Message}");
                throw;
            }
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
            try
            {
                Digits = Util.GetDesiredProperty<int>(jobj, "digits", true);
                OutputFormat = Util.GetDesiredProperty<string>(jobj, "output_format", true);
            }
            catch (Exception ex)
            {
                _logger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: DataEditOptionFloatRounding caused by {ex.Message}");
                throw;
            }
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: DataEditOptionFloatRounding");
        }
    }

    class DataEditOptionRound : DataEditOptionFloatRounding
    {
        public DataEditOptionRound(JObject jobj) : base(jobj) { }
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
        public int Start_index { get; set; } = 0;
        // 元private。シリアライズするためにpublicに変更
        public int Length { get; set; } = -1;
        static ILogger _logger { get; } = LoggerFactory.GetLogger(typeof(DataEditOptionSubstring));

        public DataEditOptionSubstring(JObject jobj)
        {
            _logger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: DataEditOptionSubstring");

            Start_index = Util.GetDesiredProperty<int>(jobj, "startindex", true, condition: (value) => value >= 0);
            Length = Util.GetDesiredProperty<int>(jobj, "length", false, -1, condition: (value) => value >= 0 || value == -1);

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