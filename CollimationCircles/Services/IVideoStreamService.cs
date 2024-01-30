using Renci.SshNet;
using System;

namespace CollimationCircles.Services
{
    public interface IVideoStreamService
    {
        public SshClient CreateSslClient(string sshHost, int sshPort, string username, string password);

        public void CloseSslClient(SshClient sslClient);

        public void OpenVLCStream(SshClient sslClient, string device);
    }
}
