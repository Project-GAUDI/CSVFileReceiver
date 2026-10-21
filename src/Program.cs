namespace IotedgeV2CSVFileReceiver
{
    using TICO.GAUDI.Commons;
    using System.Threading.Tasks;

    internal class Program
    {
        static ILogger MyLogger { get; } = LoggerFactory.GetLogger(typeof(Program));

        static async Task Main(string[] args)
        {
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"Start Main");

            await using (var appMain = new MyApplicationMain())
            {
                await using (var appEngine = ApplicationEngineFactory.GetEngine())
                {
                    appEngine.SetApplication(appMain);
                    await appEngine.RunAsync().ConfigureAwait(false);
                }
            }

            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"End Main");
        }
    }
}
