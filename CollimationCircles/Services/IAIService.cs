using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    interface IAIService
    {
        public Task<string> AnalyzeImageAsync(string openApiKey, string pathToLocalImage, string openAiModel = "gpt-4o");
        public Task<string> AnalyzeCollimationWithGeminiAsync(
            string googleApiKey,
            string pathToImageFile,
            string modelName = "gemini-pro-vision");
    }
}
