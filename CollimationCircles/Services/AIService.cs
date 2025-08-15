using CommunityToolkit.Diagnostics;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Files;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CollimationCircles.Services;
public class AIService : IAIService
{
    public async Task<string> AnalyzeImageAsync(string openApiKey, string pathToLocalImage, string openAiModel = "gpt-4o")
    {
        Guard.IsNotNullOrEmpty(openApiKey);
        Guard.IsNotNullOrEmpty(pathToLocalImage);

        if (!File.Exists(pathToLocalImage))
        { 
            throw new FileNotFoundException($"Image file not found: {pathToLocalImage}", pathToLocalImage);
        }

        #pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        try
        {
            var openAI = new OpenAIClient(openApiKey);
            var fileClient = openAI.GetOpenAIFileClient();
            var assistantClient = openAI.GetAssistantClient();

            // 1) Upload image for vision analysis
            var imageFileResult = await fileClient.UploadFileAsync(pathToLocalImage, FileUploadPurpose.Vision);
            var imageFile = imageFileResult.Value;

            // 2) Create assistant
            var assistantResult = await assistantClient.CreateAssistantAsync(
                openAiModel,
                new AssistantCreationOptions
                {
                    Instructions =
                        "You are a telescope collimation expert. Analyze a single image that may be either:\n" +
                        "(A) a defocused star test (intra-/extra-focal), or\n" +
                        "(B) an optical-path/tube view (Cheshire/sight-tube view).\n\n" +
                        "Produce a DETAILED report with:\n" +
                        "1) Verdict: Well collimated, slightly miscollimated, or significantly miscollimated.\n" +
                        "2) Key visual cues.\n" +
                        "3) Estimated offsets/tilt with severity 0–10.\n" +
                        "4) Step-by-step adjustments.\n" +
                        "5) Confidence level and caveats.\n" +
                        "Do NOT invent hardware not visible."
                });
            var assistant = assistantResult.Value;

            // 3) Create a thread
            string userPrompt = "Analyze the telescope collimation quality in this image and provide a detailed technician-grade report.";
            var threadResult = await assistantClient.CreateThreadAsync(new ThreadCreationOptions
            {
                InitialMessages =
                {
                    new ThreadInitializationMessage(
                        MessageRole.User,
                        new MessageContent[]
                        {
                            userPrompt,
                            MessageContent.FromImageFileId(imageFile.Id)
                        })
                }
            });
            var thread = threadResult.Value;

            // 4) Create a run
            var runResult = await assistantClient.CreateRunAsync(thread.Id, assistant.Id);
            var run = runResult.Value;

            // 5) Poll until run completes
            while (!run.Status.IsTerminal)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                run = (await assistantClient.GetRunAsync(run.ThreadId, run.Id)).Value;
            }

            // 6) Iterate messages
            var sb = new StringBuilder();
            int messageCount = 0;

            await foreach (var msg in assistantClient.GetMessagesAsync(
                run.ThreadId,
                new MessageCollectionOptions { Order = MessageCollectionOrder.Ascending }))
            {
                messageCount++;
                if (msg.Role == MessageRole.Assistant && msg.Content != null)
                {
                    foreach (var part in msg.Content)
                    {
                        if (!string.IsNullOrWhiteSpace(part.Text))
                        {
                            sb.AppendLine(part.Text.Trim());
                            sb.AppendLine();
                        }
                    }
                }
            }

            var resultText = sb.ToString().Trim();

            if (string.IsNullOrEmpty(resultText))
            {
                var errorDetails = new StringBuilder();
                errorDetails.AppendLine("No analysis text was returned by the assistant.");
                errorDetails.AppendLine($"Run Status: {run.Status}");
                errorDetails.AppendLine($"Run Completed At: {run.CompletedAt}");
                if (!string.IsNullOrEmpty(run.LastError?.Message))
                {
                    errorDetails.AppendLine($"Last Error: {run.LastError.Message} (Code: {run.LastError.Code})");
                }
                errorDetails.AppendLine($"Total messages retrieved: {messageCount}");
                errorDetails.AppendLine("Possible causes:");
                errorDetails.AppendLine(" - Model did not return a text part (maybe only returned tool calls).");
                errorDetails.AppendLine(" - Vision model could not interpret the image.");
                errorDetails.AppendLine(" - Prompt may have been too vague.");
                errorDetails.AppendLine(" - An internal error occurred during processing.");
                return errorDetails.ToString();
            }

            return resultText;
        }
        catch (Exception ex)
        {
            return $"Error during collimation analysis: {ex.Message}";
        }
    }

    #pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}
