using Renci.SshNet;
using System;

namespace CollimationCircles.Services
{
    public class VideoStreamService : IVideoStreamService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public SshClient CreateSslClient(string sshHost, int sshPort, string username, string password)
        {
            SshClient sshClient = new(sshHost, sshPort, username, password);
            sshClient.Connect();

            logger.Info($"SSL client connection for user '{sshClient?.ConnectionInfo.Username}' opened");

            return sshClient!;
        }

        public void CloseSslClient(SshClient sslClient)
        {
            sslClient?.Disconnect();
            sslClient?.Dispose();

            logger.Info($"SSL client connection closed");
        }

        public void OpenVLCStream(SshClient sslClient, string device)
        {
            try
            {
                if (sslClient.IsConnected)
                {
                    logger.Info(sslClient.ConnectionInfo.ServerVersion);
                    logger.Info($"Remote SSH connected with user '{sslClient.ConnectionInfo.Username}'");

                    var commandString = $"cvlc 'v4l2:///dev/{device}' --sout '#transcode{{vcodec=wmv2, vb=4096}}:http{{mux=asf, dst=0.0.0.0:8080}}'";
                    var cmd = sslClient.CreateCommand(commandString);
                    var cmdResult = cmd.BeginExecute();

                    logger.Info($"SSH Command '{commandString}' executed");                    
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
        }
    }
}
