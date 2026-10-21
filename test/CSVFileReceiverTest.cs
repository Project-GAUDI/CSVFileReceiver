using System;
using Xunit;
using System.Threading.Tasks;
using TICO.GAUDI.Commons;
using TICO.GAUDI.Commons.Test;

namespace IotedgeV2CSVFileReceiver.Test
{
    public class CSVFileReceiverTest
    {
        private readonly ITestOutputHelper _output;

        public CSVFileReceiverTest(ITestOutputHelper output)
        {
            _output = output;
        }

        /// xxx のテスト（スキップ）
        /// テスト内容：必ずエラー
        [Fact(Skip="template")]
        public void MethodTest_Test001(){
            // Given
            #region
            #endregion

            // When
            #region
            #endregion

            // Then
            #region
            Assert.True(false);
            #endregion
        }


        /// xxx のテスト
        /// テスト内容：０より１が大きいか？
        [Fact]
        public void MethodTest_Test002(){
            // Given
            #region
            #endregion

            // When
            #region
            #endregion

            // Then
            #region
            Assert.True(0<1);
            #endregion
        }


        /// xxx の連続テスト
        /// テスト内容：自然数か判定（最後に失敗）
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        // [InlineData(-1)] // 失敗ケース
        public void MethodTest_Test003(int value){
            // Given
            #region
            #endregion

            // When
            #region
            #endregion

            // Then
            #region
            Assert.True( 0 < value );
            #endregion
        }
    }
}