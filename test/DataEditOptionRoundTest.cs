using System;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IotedgeV2CSVFileReceiver.Test
{
    public class DataEditOptionRoundTest
    {
        [Theory]
        [InlineData("1234", -1, "1230")]
        [InlineData("1235", -1, "1240")]
        [InlineData("1250", -2, "1300")]
        [InlineData("1249", -2, "1200")]
        [InlineData("-125", -1, "-130")]
        [InlineData("-124", -1, "-120")]
        [InlineData("12.345", 2, "12.35")]
        [InlineData("1.5", 0, "2")]
        public void RoundSupportsPositiveAndNegativeDigits(string input, int digits, string expected)
        {
            var option = new DataEditOptionRound(
                new JObject
                {
                    ["digits"] = digits,
                    ["output_format"] = "0.##"
                });

            Assert.Equal(expected, option.GetEditValue(input));
        }

        [Theory]
        [InlineData(-29)]
        [InlineData(29)]
        public void InvalidDigitsAreRejected(int digits)
        {
            var config = new JObject
            {
                ["digits"] = digits,
                ["output_format"] = "0"
            };
            Assert.Throws<ArgumentOutOfRangeException>(() => new DataEditOptionRound(config));
        }
    }
}
