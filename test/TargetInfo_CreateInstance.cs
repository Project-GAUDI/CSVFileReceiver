using System;
using Xunit;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CSVFileReceiver.Test
{
    public class TargetInfo_CreateInstance
    {
        private readonly ITestOutputHelper _output;

        public TargetInfo_CreateInstance(ITestOutputHelper output)
        {
            _output = output;
        }

        private string getDesiredPropertiesStr(string type="")
        {
            const string header="{";
            const string common= @"
                                    ""file_type"": ""standard"",
                                    ""output_name"": ""out1"",
                                    ""filter"": ""^005_017_(J|j)(P|p)0130100(G|g)30102_.+\\.(C|c)(S|s)(V|v)$"",
                                    ""encode"": ""shift-jis"",
                                    ""delimiter"": "","",
                                    ""data_start_line"": 2,
                                    ""eof_enabled"": false,
                                    ""after_process"": ""delete"",
                                    ""send_header_enabled"": false
                                    ";
            const string footer= "}";

            string desiredPropertiesStr = header;
            switch (type){
                case "NoGetFrom":
                    const string nogetfrom = @"
                                                ""data_property1"":{
                                                    ""name"": ""machine_number"",
                                                    ""column"": 4
                                                },
                                            ";
                    desiredPropertiesStr += nogetfrom;
                    break;
                case "message":
                    const string message = @"
                                                ""data_property1"":{
                                                    ""name"": ""machine_number"",
                                                    ""column"": 4,
                                                    ""get_from"":""message""
                                                },
                                            ";
                    desiredPropertiesStr += message;
                    break;
                case "Message":
                    const string Message = @"
                                                ""data_property1"":{
                                                    ""name"": ""machine_number"",
                                                    ""column"": 4,
                                                    ""get_from"":""Message""
                                                },
                                            ";
                    desiredPropertiesStr += Message;
                    break;
                case "file":
                    const string file = @"
                                                ""data_property1"":{
                                                    ""name"": ""machine_number"",
                                                    ""column"": 4,
                                                    ""get_from"":""file""
                                                },
                                            ";
                    desiredPropertiesStr += file;
                    break;
                case "File":
                    const string File = @"
                                                ""data_property1"":{
                                                    ""name"": ""machine_number"",
                                                    ""column"": 4,
                                                    ""get_from"":""File""
                                                },
                                            ";
                    desiredPropertiesStr += File;
                    break;
                case "Number":
                    const string Number = @"
                                                ""data_property1"":{
                                                    ""name"": ""machine_number"",
                                                    ""column"": 4,
                                                    ""get_from"":1
                                                },
                                            ";
                    desiredPropertiesStr += Number;
                    break;
                case "OtherString":
                    const string OtherString = @"
                                                ""data_property1"":{
                                                    ""name"": ""machine_number"",
                                                    ""column"": 4,
                                                    ""get_from"":""AAA""
                                                },
                                            ";
                    desiredPropertiesStr += OtherString;
                    break;
                case "Empty":
                    const string Empty = @"
                                                ""data_property1"":{
                                                    ""name"": ""machine_number"",
                                                    ""column"": 4,
                                                    ""get_from"":""""
                                                },
                                            ";
                    desiredPropertiesStr += Empty;
                    break;
                 
                default:
                    break;
            }

            desiredPropertiesStr += common;
            desiredPropertiesStr += footer;
            
            return desiredPropertiesStr;
        }

        private string getDesiredPropertiesStr_FileType(string type="")
        {
            const string header="{";
            const string common= @"
                                    ""output_name"": ""out1"",
                                    ""filter"": ""^005_017_(J|j)(P|p)0130100(G|g)30102_.+\\.(C|c)(S|s)(V|v)$"",
                                    ""encode"": ""shift-jis"",
                                    ""delimiter"": "","",
                                    ""data_start_line"": 2,
                                    ""eof_enabled"": false,
                                    ""after_process"": ""delete"",
                                    ""send_header_enabled"": false,
                                    ""header_start_line"":1,
                                    ""header_end_line"":1,
                                    ""sampling_interval_line"":0,
                                    ""timestamp_format"":""yyyy/mm/dd""
                                    ";
            const string footer= "}";

            string desiredPropertiesStr = header;
            switch (type){
                case "standard":
                    const string standard = @"
                                                ""file_type"": ""standard"",
                                            ";
                    desiredPropertiesStr += standard;
                    break;
                case "Standard":
                    const string Standard = @"
                                                ""file_type"": ""Standard"",
                                            ";
                    desiredPropertiesStr += Standard;
                    break;
                case "aaa":
                    const string aaa = @"
                                                ""file_type"": ""aaa"",
                                            ";
                    desiredPropertiesStr += aaa;
                    break;
                case "AAA":
                    const string AAA = @"
                                                ""file_type"": ""AAA"",
                                            ";
                    desiredPropertiesStr += AAA;
                    break;
                case "pd":
                    const string pd = @"
                                                ""file_type"": ""pd"",
                                            ";
                    desiredPropertiesStr += pd;
                    break;
                case "Pd":
                    const string Pd = @"
                                                ""file_type"": ""Pd"",
                                            ";
                    desiredPropertiesStr += Pd;
                    break;
                 
                default:
                    break;
            }

            desiredPropertiesStr += common;
            desiredPropertiesStr += footer;

            return desiredPropertiesStr;
        }


        private string getDesiredPropertiesStr_AfterProcess(string type="")
        {
            const string header="{";
            const string common= @"
                                    ""file_type"": ""standard"",
                                    ""output_name"": ""out1"",
                                    ""filter"": ""^005_017_(J|j)(P|p)0130100(G|g)30102_.+\\.(C|c)(S|s)(V|v)$"",
                                    ""encode"": ""shift-jis"",
                                    ""delimiter"": "","",
                                    ""data_start_line"": 2,
                                    ""eof_enabled"": false,
                                    ""send_header_enabled"": false
                                    ";
            const string footer= "}";

            string desiredPropertiesStr = header;
            switch (type){
                case "move":
                    const string move = @"
                                                ""after_process"": ""move"",
                                            ";
                    desiredPropertiesStr += move;
                    break;
                case "Move":
                    const string Move = @"
                                                ""after_process"": ""Move"",
                                            ";
                    desiredPropertiesStr += Move;
                    break;
                case "delete":
                    const string delete = @"
                                                ""after_process"": ""delete"",
                                            ";
                    desiredPropertiesStr += delete;
                    break;
                case "Delete":
                    const string Delete = @"
                                                ""after_process"": ""Delete"",
                                            ";
                    desiredPropertiesStr += Delete;
                    break;
                 
                default:
                    break;
            }

            desiredPropertiesStr += common;
            desiredPropertiesStr += footer;

            return desiredPropertiesStr;
        }

        private string getDesiredPropertiesStr_DataEditOption(string type="")
        {
            const string header="{";
            const string common= @"
                                    ""file_type"": ""standard"",
                                    ""output_name"": ""out1"",
                                    ""filter"": ""^005_017_(J|j)(P|p)0130100(G|g)30102_.+\\.(C|c)(S|s)(V|v)$"",
                                    ""encode"": ""shift-jis"",
                                    ""delimiter"": "","",
                                    ""data_start_line"": 2,
                                    ""eof_enabled"": false,
                                    ""after_process"": ""delete"",
                                    ""send_header_enabled"": false
                                    ";
            const string footer= "}";

            string desiredPropertiesStr = header;
            switch (type){
                case "round":
                    const string round = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""round"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6""
                                                },
                                            ";
                    desiredPropertiesStr += round;
                    break;
                case "Round":
                    const string Round = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""Round"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6""
                                                },
                                            ";
                    desiredPropertiesStr += Round;
                    break;
                case "floor":
                    const string floor = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""floor"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6""
                                                },
                                            ";
                    desiredPropertiesStr += floor;
                    break;
                case "Floor":
                    const string Floor = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""Floor"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6""
                                                },
                                            ";
                    desiredPropertiesStr += Floor;
                    break;
                case "truncate":
                    const string truncate = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""truncate"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6""
                                                },
                                            ";
                    desiredPropertiesStr += truncate;
                    break;
                case "Truncate":
                    const string Truncate = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""Truncate"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6""
                                                },
                                            ";
                    desiredPropertiesStr += Truncate;
                    break;
                case "ceiling":
                    const string ceiling = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""ceiling"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6""
                                                },
                                            ";
                    desiredPropertiesStr += ceiling;
                    break;
                case "Ceiling":
                    const string Ceiling = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""Ceiling"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6""
                                                },
                                            ";
                    desiredPropertiesStr += Ceiling;
                    break;
                case "substring":
                    const string substring = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""substring"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6"",
                                                    ""startindex"":1
                                                },
                                            ";
                    desiredPropertiesStr += substring;
                    break;
                case "Substring":
                    const string Substring = @"
                                                ""data_edit_option1"":{
                                                    ""mode"": ""Substring"",
                                                    ""column"":1,
                                                    ""digits"":1,
                                                    ""output_format"":""E6"",
                                                    ""startindex"":1
                                                },
                                            ";
                    desiredPropertiesStr += Substring;
                    break;
             
                default:
                    break;
            }

            desiredPropertiesStr += common;
            desiredPropertiesStr += footer;
            
            return desiredPropertiesStr;
        }


        [Fact(DisplayName = "正常系:TargetInfoインスタンス生成→TargetInfoインスタンス生成")]
        public void SimpleValue_TargetInfoCreated()
        {
            string desiredPropertyStr = getDesiredPropertiesStr();
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.IsAssignableFrom<TargetInfo>(result);
        }

        

        [Fact(DisplayName = "正常系:get_fromがない→Get_Fromはmessage")]
        public void NoGetFrom_GetFromIsMessage()
        {
            string desiredPropertyStr = getDesiredPropertiesStr("NoGetFrom");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(GetFrom.Message, result.DataProperties[0].Get_From);
        }

        [Fact(DisplayName = "正常系:get_fromがmessage→Get_Fromはmessage")]
        public void GetFromIsmessage_GetFromIsMessage()
        {
            string desiredPropertyStr = getDesiredPropertiesStr("message");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(GetFrom.Message, result.DataProperties[0].Get_From);
        }

        [Fact(DisplayName = "正常系:get_fromがMessage→Get_Fromはmessage")]
            public void GetFromIsMessage_GetFromIsMessage()
        {
            string desiredPropertyStr = getDesiredPropertiesStr("Message");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(GetFrom.Message, result.DataProperties[0].Get_From);
        }


        [Fact(DisplayName = "正常系:get_fromがfile→Get_Fromはfile")]
        public void GetFromIsfile_GetFromIsFile()
        {
            string desiredPropertyStr = getDesiredPropertiesStr("file");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(GetFrom.File, result.DataProperties[0].Get_From);
        }

        [Fact(DisplayName = "正常系:get_fromがFile→Get_Fromはfile")]
        public void GetFromIsFile_GetFromIsFile()
        {
            string desiredPropertyStr = getDesiredPropertiesStr("File");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(GetFrom.File, result.DataProperties[0].Get_From);
        }

        [Fact(DisplayName = "異常系:get_fromが1→例外")]
        public void GetFromIsNumber_ExceptionThrown()
        {
            string desiredPropertyStr = getDesiredPropertiesStr("Number");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            Assert.Throws<ArgumentException>(() => { TargetInfo.CreateInstance(jobj, false); });
        }

        [Fact(DisplayName = "異常系:get_fromがAAA→例外")]
        public void GetFromIsOtherString_ExceptionThrown()
        {
            string desiredPropertyStr = getDesiredPropertiesStr("OtherString");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            Assert.Throws<ArgumentException>(() => { TargetInfo.CreateInstance(jobj, false); });
        }

        [Fact(DisplayName = "異常系:get_fromが空文字→例外")]
        public void GetFromIsEmpty_ExceptionThrown()
        {
            string desiredPropertyStr = getDesiredPropertiesStr("Empty");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            Assert.Throws<ArgumentException>(() => { TargetInfo.CreateInstance(jobj, false); });
        }
        

        [Fact(DisplayName = "正常系:file_typeがstandard→FileTypeはStandard")]
        public void FileTypeIsstandard_FileTypeIsStandard()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_FileType("standard");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(FileType.Standard, result.FileType);
        }
        
        [Fact(DisplayName = "正常系:file_typeがStandard→FileTypeはStandard")]
        public void FileTypeIsStandard_FileTypeIsStandard()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_FileType("Standard");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(FileType.Standard, result.FileType);
        }

        [Fact(DisplayName = "正常系:file_typeがaaa→FileTypeはAAA")]
        public void FileTypeIsaaa_FileTypeIsAAA()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_FileType("aaa");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(FileType.AAA, result.FileType);
        }

        [Fact(DisplayName = "正常系:file_typeがAAA→FileTypeはAAA")]
        public void FileTypeIsAAA_FileTypeIsAAA()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_FileType("AAA");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(FileType.AAA, result.FileType);
        }

        [Fact(DisplayName = "正常系:file_typeがpd→FileTypeはProductDevelopment")]
        public void FileTypeIspd_FileTypeIsProductDevelopment()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_FileType("pd");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(FileType.ProductDevelopment, result.FileType);
        }

        [Fact(DisplayName = "正常系:file_typeがPd→FileTypeはProductDevelopment")]
        public void FileTypeIsPd_FileTypeIsProductDevelopment()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_FileType("Pd");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, false);
            Assert.Equal(FileType.ProductDevelopment, result.FileType);
        }


        [Fact(DisplayName = "正常系:after_processがmove→AfterProcessはMove")]
        public void AfterProcessIsmove_AfterProcessIsMove()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_AfterProcess("move");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.Equal(AfterProcess.Move, result.AfterProcess);
        }

        [Fact(DisplayName = "正常系:after_processがMove→AfterProcessはMove")]
        public void AfterProcessIsMove_AfterProcessIsMove()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_AfterProcess("Move");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.Equal(AfterProcess.Move, result.AfterProcess);
        }

        [Fact(DisplayName = "正常系:after_processがdelete→AfterProcessはDelete")]
        public void AfterProcessIsdelete_AfterProcessIsDelete()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_AfterProcess("delete");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.Equal(AfterProcess.Delete, result.AfterProcess);
        }

        [Fact(DisplayName = "正常系:after_processがDelete→AfterProcessはDelete")]
        public void AfterProcessIsDelete_AfterProcessIsDelete()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_AfterProcess("Delete");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.Equal(AfterProcess.Delete, result.AfterProcess);
        }

        [Fact(DisplayName = "正常系:modeがround→DataEditOptionの型がDataEditOptionRound")]
        public void ModeIsround_DataEditOptionIsDataEditOptionRound()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("round");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionRound>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがRound→DataEditOptionの型がDataEditOptionRound")]
        public void ModeIsRound_DataEditOptionIsDataEditOptionRound()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("Round");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionRound>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがfloor→DataEditOptionの型がDataEditOptionFloor")]
        public void ModeIsfloor_DataEditOptionIsDataEditOptionFloor()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("floor");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionFloor>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがFloor→DataEditOptionの型がDataEditOptioFloor")]
        public void ModeIsFloor_DataEditOptionIsDataEditOptionFloor()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("Floor");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionFloor>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがtrcuncate→DataEditOptionの型がDataEditOptionTruncate")]
        public void ModeIstrcuncate_DataEditOptionIsDataEditOptionTrcuncate()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("truncate");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionTruncate>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがTrcuncate→DataEditOptionの型がDataEditOptionTruncate")]
        public void ModeIsTrcuncate_DataEditOptionIsDataEditOptionTrcuncate()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("Truncate");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionTruncate>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがceiling→DataEditOptionの型がDataEditOptionCeiling")]
        public void ModeIsceiling_DataEditOptionIsDataEditOptionCeiling()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("ceiling");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionCeiling>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがCeiling→DataEditOptionの型がDataEditOptionCeiling")]
        public void ModeIsCeiling_DataEditOptionIsDataEditOptionFloor()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("Ceiling");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionCeiling>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがsubstring→DataEditOptionの型がDataEditOptionSubstring")]
        public void ModeIssubstring_DataEditOptionIsDataEditOptionSubstring()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("substring");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionSubstring>(result.DataEditOptions[0]);
        }

        [Fact(DisplayName = "正常系:modeがSubstring→DataEditOptionの型がDataEditOptionSubstring")]
        public void ModeIsSubstring_DataEditOptionIsDataEditOptionSubstring()
        {
            string desiredPropertyStr = getDesiredPropertiesStr_DataEditOption("Substring");
            JObject jobj = JObject.Parse(desiredPropertyStr);
            TargetInfo result = TargetInfo.CreateInstance(jobj, true);
            Assert.IsAssignableFrom<DataEditOptionSubstring>(result.DataEditOptions[0]);
        }

    }
}
