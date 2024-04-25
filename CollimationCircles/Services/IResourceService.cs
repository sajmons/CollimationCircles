namespace CollimationCircles.Services
{
    public interface IResourceService
    {
        void Translate(string targetLanguage);
        string TryGetString(string resourceKey);
        string TryGet(string resourceKey);
    }
}
