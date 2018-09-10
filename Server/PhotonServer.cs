using System.IO;
using ExitGames.Logging;
using ExitGames.Logging.Log4Net;
using log4net.Config;
using Photon.SocketServer;

namespace PhotonIntro
{
    public class PhotonServer : ApplicationBase
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            Log.Debug("Peer Connection");
            return new UnityClient(initRequest);
        }
        
        protected override void Setup()
        {
            var configFileInfo = new FileInfo(Path.Combine(BinaryPath, "log4net.config"));
            if (configFileInfo.Exists)
            {
                LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
                XmlConfigurator.ConfigureAndWatch(configFileInfo);
            }
            Game.Instance = new Game();
            Game.Instance.Startup();
        }

        protected override void TearDown()
        {
            Game.Instance.Shutdown();
        }
    }
}
