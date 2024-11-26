namespace IotedgeV2CSVFileReceiver
{
    using TICO.GAUDI.Commons;

    internal class Program
    {
        static ILogger MyLogger { get; } = LoggerFactory.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"Start Main");

            using( IApplicationMain appMain = new MyApplicationMain())
            {
                using (IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine() )
                {
                    appEngine.SetApplication(appMain);

                    appEngine.RunAsync().Wait();
                }
            }

            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"End Main");
        }
    }
}
