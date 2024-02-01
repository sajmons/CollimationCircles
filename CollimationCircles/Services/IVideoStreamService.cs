namespace CollimationCircles.Services
{
    public interface IVideoStreamService
    {
        //public SshClient CreateSslClient(string sshHost, int sshPort, string username, string password);

        public void CloseVideoStream(bool isUVCCamera);

        public void OpenVideoStream(string uvcDevice, bool isUVCCamera, string address);
    }
}
