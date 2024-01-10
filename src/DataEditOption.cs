using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TICO.GAUDI.Commons;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("CSVFileReceiver.Test")]

namespace CSVFileReceiver
{
    public abstract class DataEditOption
    {
        public int Column { get; private set; }

        public string Mode {
            get{
                return this.GetType().ToString();
            }
        }

        public static DataEditOption CreateInstance(JObject jobj)
        {
            int column = Util.GetRequiredValue<int>(jobj, "column");
            if (column < 1)
            {
                throw new Exception($"column can't set {column}");
            }

            string mode = Util.GetRequiredValue<string>(jobj, "mode");
            if (string.IsNullOrEmpty(mode))
            {
                throw new Exception($"Property 'mode' dose not exist.");
            }

            mode=mode.ToLower();

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
                throw new Exception($"mode:{mode} is not supported.");
            }

            ret.Column = column;
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

        public DataEditOptionFloatRounding(JObject jobj)
        {
            Digits = Util.GetRequiredValue<int>(jobj, "digits");
            if (Digits < 0)
            {
                throw new Exception($"digits can't set {Digits}");
            }

            OutputFormat = Util.GetRequiredValue<string>(jobj, "output_format");
            if (string.IsNullOrEmpty(OutputFormat))
            {
                throw new Exception($"Property 'output_format' dose not exist.");
            }
        }
    }

    class DataEditOptionRound : DataEditOptionFloatRounding
    {
        public DataEditOptionRound(JObject jobj) : base (jobj){ }

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
        public int Start_index { get; set; } = -1;
        // 元private。シリアライズするためにpublicに変更
        public int Length { get; set; } = -1;

        public DataEditOptionSubstring(JObject jobj)
        {
            Start_index = Util.GetRequiredValue<int>(jobj, "startindex");
            if (Start_index < 0)
            {
                throw new Exception($"startindex can't set {Start_index}");
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
        }

        public override string GetEditValue(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "";
            }
            string ret = input;
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
            return ret;
        }
    }
}
