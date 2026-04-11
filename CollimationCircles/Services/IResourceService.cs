namespace CollimationCircles.Services
{
    public interface IResourceService
    {
        void Translate(string language = "en-US");
        string TryGetString(string resourceKey);
        string TryGet(string resourceKey);
    }
}
