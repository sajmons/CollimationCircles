using OpenAI.Assistants;
using OpenAI.Files;
using OpenAI;
using System;
using System.ClientModel;
using System.IO;
using CommunityToolkit.Diagnostics;

namespace CollimationCircles.Services;
public class AIService : IAIService
{
    public void AnalyzeImage(string apiKey, string pathToImage)
    {
        Guard.IsNotNullOrEmpty(apiKey);
        Guard.IsNotNullOrEmpty(pathToImage);

        if (!File.Exists(pathToImage))
        { 
            throw new FileNotFoundException($"Image file not found: {pathToImage}", pathToImage);
        }

        OpenAIClient openAIClient = new(apiKey);
        OpenAIFileClient fileClient = openAIClient.GetOpenAIFileClient();
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        AssistantClient assistantClient = openAIClient.GetAssistantClient();

        OpenAIFile pictureOfAppleFile = fileClient.UploadFile(
            pathToImage,
            FileUploadPurpose.Vision);

        Assistant assistant = assistantClient.CreateAssistant(
            "gpt-4o",
            new AssistantCreationOptions()
            {
                Instructions = "When asked a question, attempt to answer very concisely. "
                    + "Prefer one-sentence answers whenever feasible."
            });

        AssistantThread thread = assistantClient.CreateThread(new ThreadCreationOptions()
        {
            InitialMessages =
                {
                    new ThreadInitializationMessage(
                        MessageRole.User,
                        [
                            "Attached image is from telescope and it contains defocused star. Please analyze it to see if telescope is properly collimated:",
                            MessageContent.FromImageFileId(pictureOfAppleFile.Id)
                        ]),
                }
        });

        CollectionResult<StreamingUpdate> streamingUpdates = assistantClient.CreateRunStreaming(
            thread.Id,
            assistant.Id,
            new RunCreationOptions()
            {
                AdditionalInstructions = "When possible, try to sneak in puns if you're asked to compare things.",
            });

        foreach (StreamingUpdate streamingUpdate in streamingUpdates)
        {
            if (streamingUpdate.UpdateKind == StreamingUpdateReason.RunCreated)
            {
                Console.WriteLine($"--- Run started! ---");
            }
            if (streamingUpdate is MessageContentUpdate contentUpdate)
            {
                Console.Write(contentUpdate.Text);
            }
        }

        // Delete temporary resources, if desired
        _ = fileClient.DeleteFile(pictureOfAppleFile.Id);
        _ = assistantClient.DeleteThread(thread.Id);
        _ = assistantClient.DeleteAssistant(assistant.Id);
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }
}
