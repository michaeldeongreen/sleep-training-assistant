using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Azure.Identity;
using BlazorApp.Models;
using System.ClientModel;
using System.Text.Json;
using System.Text;

namespace BlazorApp.Services;

public class AzureOpenAIService
{
    private readonly ChatClient? _client;
    private readonly PdfService _pdfService;
    private readonly TimeService _timeService;
    private readonly TableStorageService _tableStorageService;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly bool _isConfigured;

    public AzureOpenAIService(
        IConfiguration configuration,
        PdfService pdfService,
        TimeService timeService,
        TableStorageService tableStorageService,
        ILogger<AzureOpenAIService> logger)
    {
        _pdfService = pdfService;
        _timeService = timeService;
        _tableStorageService = tableStorageService;
        _logger = logger;

        var endpoint = configuration["AzureOpenAI:Endpoint"] 
            ?? configuration["AZURE_OPENAI_ENDPOINT"];

        var deploymentName = configuration["AzureOpenAI:DeploymentName"] 
            ?? configuration["AZURE_OPENAI_DEPLOYMENT_NAME"] 
            ?? "4o-mini";

        if (string.IsNullOrEmpty(endpoint))
        {
            _logger.LogWarning("Azure OpenAI endpoint not configured - chat will not work");
            _isConfigured = false;
            return;
        }

        // Use Entra ID authentication (Managed Identity or DefaultAzureCredential)
        // This works when API key authentication is disabled in Azure AI Foundry
        try
        {
            var credential = new DefaultAzureCredential();
            var azureClient = new AzureOpenAIClient(new Uri(endpoint), credential);
            _client = azureClient.GetChatClient(deploymentName);
            _isConfigured = true;
            _logger.LogInformation("Azure OpenAI configured with Entra ID authentication");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Azure OpenAI client");
            _isConfigured = false;
        }
    }

    public async Task<ChatResponse> GetChatResponseAsync(List<Models.ChatMessage> conversationHistory, string userMessage)
    {
        if (!_isConfigured || _client == null)
        {
            return new ChatResponse
            {
                Message = "Azure OpenAI is not configured. Please set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY in your configuration.",
                Success = false,
                Error = "Not configured"
            };
        }

        try
        {
            // Get PDF content
            var pdfContent = await _pdfService.GetPdfContentAsync();

            // Get today's sleep tracking data
            var sleepTrackingData = await _tableStorageService.GetTodayDataAsTextAsync();

            // Log what we got
            _logger.LogInformation("PDF content length: {Length}", pdfContent?.Length ?? 0);
            _logger.LogInformation("Sleep tracking data length: {Length}", sleepTrackingData?.Length ?? 0);

            // Get current time in CDT
            var currentTime = _timeService.GetCurrentCentralTime();
            var currentTimeString = currentTime.ToString("dddd, MMMM dd, yyyy h:mm tt");

            // Build the system prompt - this is critical for the AI to understand its capabilities
            var systemPrompt = $@"You are a helpful AI assistant for a sleep training application for Savannah.

You have access to the sleep training guide document and today's sleep tracking data.

CURRENT TIME: {currentTimeString} (Central Daylight Time)

PROVIDED DOCUMENTS:

=== SLEEP TRAINING GUIDE (PDF) ===
{(string.IsNullOrEmpty(pdfContent) ? "PDF content not yet loaded. Please upload the PDF file using the Upload Documents page." : pdfContent)}

=== TODAY'S SLEEP TRACKING DATA ===
{sleepTrackingData}

=== END OF PROVIDED DOCUMENTS ===

INSTRUCTIONS:
- Use information from the sleep training guide to answer questions
- Reference today's sleep tracking data when discussing Savannah's current schedule
- When the user mentions sleep-related times or events for TODAY, use the update_sleep_tracking tool to save them
- When the user asks about PAST dates (yesterday, last week, specific dates), use the query_sleep_history tool to retrieve historical data
- Be helpful, supportive, and informative about sleep training
- If asked about specific data in the documents, check if they are loaded first
- **IMPORTANT**: Always format your responses using Markdown syntax:
  * Use **bold** for emphasis
  * Use tables (| Header | Header |) for structured data
  * Use bullet points (-) or numbered lists (1.) where appropriate
  * Use code blocks (```) for examples
  * Use headers (##) to organize longer responses

UNDERSTANDING USER INPUT:
When users say things like:
- ""She woke up at 7 AM"" -> update WakeUp field (TODAY)
- ""Put her in crib at 9:30"" (context: nap 1) -> update Nap1TimePutInCrib (TODAY)
- ""She fell asleep at 9:45"" (context: nap 1) -> update Nap1SleepStart (TODAY)
- ""She woke from nap 1 at 10:30"" -> update Nap1Finish (TODAY)
- ""Fed her at 6 PM"" -> update FeedTime (TODAY)
- ""Had a rough night"" -> update Notes (TODAY)
- ""What time did she wake up yesterday?"" -> use query_sleep_history with yesterday's date
- ""How were her naps this week?"" -> use query_sleep_history with date range for the past 7 days
- ""Show me Tuesday's sleep data"" -> use query_sleep_history with the specific date

Pay attention to context clues to determine which nap (1, 2, or 3) they're referring to and whether they're talking about today or a past date.";

            // Define the function/tool for updating sleep tracking
            // BEST PRACTICE: Single tool that accepts multiple field updates in one call
            // This reduces API calls, improves performance, and ensures atomic updates
            var updateSleepTrackingTool = ChatTool.CreateFunctionTool(
                functionName: "update_sleep_tracking",
                functionDescription: "Updates one or more fields in today's sleep tracking record for Savannah. Use this when the user provides information about wake times, nap times, feeding times, or notes. You can update multiple fields in a single call by providing an object with all the fields that need updating.",
                functionParameters: BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""WakeUp"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah woke up for the day (e.g., '7:00 AM')""
                        },
                        ""Nap1TimePutInCrib"": {
                            ""type"": ""string"",
                            ""description"": ""Time put in crib for nap 1""
                        },
                        ""Nap1SleepStart"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah fell asleep for nap 1""
                        },
                        ""Nap1Finish"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah woke up from nap 1""
                        },
                        ""Nap2TimePutInCrib"": {
                            ""type"": ""string"",
                            ""description"": ""Time put in crib for nap 2""
                        },
                        ""Nap2SleepStart"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah fell asleep for nap 2""
                        },
                        ""Nap2Finish"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah woke up from nap 2""
                        },
                        ""Nap3TimePutInCrib"": {
                            ""type"": ""string"",
                            ""description"": ""Time put in crib for nap 3""
                        },
                        ""Nap3SleepStart"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah fell asleep for nap 3""
                        },
                        ""Nap3Finish"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah woke up from nap 3""
                        },
                        ""BedtimeTimePutInCrib"": {
                            ""type"": ""string"",
                            ""description"": ""Time put in crib for bedtime/night sleep""
                        },
                        ""BedtimeTimeSleepStart"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah fell asleep for bedtime/night""
                        },
                        ""BedtimeWake1StartFinish"": {
                            ""type"": ""string"",
                            ""description"": ""Bedtime wake up 1: start and finish time (e.g., '11:30 PM - 11:45 PM')""
                        },
                        ""BedtimeWake2StartFinish"": {
                            ""type"": ""string"",
                            ""description"": ""Bedtime wake up 2: start and finish time""
                        },
                        ""BedtimeWake3StartFinish"": {
                            ""type"": ""string"",
                            ""description"": ""Bedtime wake up 3: start and finish time""
                        },
                        ""BedtimeWake4StartFinish"": {
                            ""type"": ""string"",
                            ""description"": ""Bedtime wake up 4: start and finish time""
                        },
                        ""BedtimeWake5StartFinish"": {
                            ""type"": ""string"",
                            ""description"": ""Bedtime wake up 5: start and finish time""
                        },
                        ""BedtimeWake6StartFinish"": {
                            ""type"": ""string"",
                            ""description"": ""Bedtime wake up 6: start and finish time""
                        },
                        ""FeedTime"": {
                            ""type"": ""string"",
                            ""description"": ""Time Savannah was fed""
                        },
                        ""Notes"": {
                            ""type"": ""string"",
                            ""description"": ""General notes or observations about sleep/behavior. When adding a new note, retrieve the existing Notes value first, then append the new note as a bullet point (- New note text). If Notes is empty or 'None', start with the first bullet point.""
                        }
                    },
                    ""additionalProperties"": false,
                    ""description"": ""Provide only the fields that need to be updated. You can update one field or many fields in a single call.""
                }")
            );

            // Define tool for querying historical sleep data
            var querySleepHistoryTool = ChatTool.CreateFunctionTool(
                functionName: "query_sleep_history",
                functionDescription: "Retrieves sleep tracking data for a specific date or date range. Use this when the user asks about past days (yesterday, last week, specific dates, etc.). For today's data, it's already provided in the system prompt.",
                functionParameters: BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""startDate"": {
                            ""type"": ""string"",
                            ""description"": ""Start date in YYYYMMDD format (e.g., '20251121' for November 21, 2025). Calculate this from the current date based on user's query (yesterday, last Tuesday, etc.)""
                        },
                        ""endDate"": {
                            ""type"": ""string"",
                            ""description"": ""Optional end date in YYYYMMDD format for range queries. If omitted, only the startDate is queried.""
                        }
                    },
                    ""required"": [""startDate""]
                }")
            );

            // Prepare messages - use fully qualified name to avoid ambiguity
            var messages = new List<OpenAI.Chat.ChatMessage>();
            messages.Add(OpenAI.Chat.ChatMessage.CreateSystemMessage(systemPrompt));

            // Add conversation history
            foreach (var msg in conversationHistory)
            {
                if (msg.Role == "user")
                    messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(msg.Content));
                else if (msg.Role == "assistant")
                    messages.Add(OpenAI.Chat.ChatMessage.CreateAssistantMessage(msg.Content));
            }

            // Add current user message
            messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(userMessage));

            // Configure chat options with the tools
            var chatOptions = new ChatCompletionOptions
            {
                Tools = { updateSleepTrackingTool, querySleepHistoryTool }
            };

            // Get AI response - it may call the tool
            var response = await _client.CompleteChatAsync(messages, chatOptions);
            
            var responseMessage = response.Value;
            var assistantMessageBuilder = new StringBuilder();

            // Check if AI wants to call the function/tool
            if (responseMessage.FinishReason == ChatFinishReason.ToolCalls && responseMessage.ToolCalls.Count > 0)
            {
                _logger.LogInformation("AI is calling {Count} tool(s)", responseMessage.ToolCalls.Count);

                // Add the assistant's message with tool calls to history for context
                // Create a proper assistant message from the response
                var assistantWithToolCalls = OpenAI.Chat.ChatMessage.CreateAssistantMessage(responseMessage);
                messages.Add(assistantWithToolCalls);

                // Process each tool call
                foreach (var toolCall in responseMessage.ToolCalls)
                {
                    if (toolCall.FunctionName == "update_sleep_tracking")
                    {
                        _logger.LogInformation("Tool call arguments: {Args}", toolCall.FunctionArguments.ToString());

                        // Parse the arguments - now it's an object with multiple optional fields
                        var args = JsonDocument.Parse(toolCall.FunctionArguments.ToString());
                        
                        // Actually execute the update with all provided fields
                        var updateResult = await UpdateSleepTrackingFieldsAsync(args.RootElement);

                        // Tell the AI the result of the tool call
                        messages.Add(OpenAI.Chat.ChatMessage.CreateToolMessage(toolCall.Id, updateResult));
                    }
                    else if (toolCall.FunctionName == "query_sleep_history")
                    {
                        _logger.LogInformation("Querying sleep history with arguments: {Args}", toolCall.FunctionArguments.ToString());

                        // Parse the arguments
                        var args = JsonDocument.Parse(toolCall.FunctionArguments.ToString());
                        var startDate = args.RootElement.GetProperty("startDate").GetString();
                        var endDate = args.RootElement.TryGetProperty("endDate", out var endDateProp) 
                            ? endDateProp.GetString() 
                            : null;

                        // Query historical data
                        var historicalData = await _tableStorageService.GetDateRangeAsync(startDate!, endDate);
                        var formattedData = _tableStorageService.FormatHistoricalDataAsText(historicalData);

                        // Tell the AI the result
                        messages.Add(OpenAI.Chat.ChatMessage.CreateToolMessage(toolCall.Id, formattedData));
                    }
                }

                // Get the final response from AI after it knows the tool executed
                var finalResponse = await _client.CompleteChatAsync(messages, chatOptions);
                assistantMessageBuilder.Append(finalResponse.Value.Content[0].Text);
            }
            else
            {
                // No tool calls, just a regular response
                assistantMessageBuilder.Append(responseMessage.Content[0].Text);
            }

            return new ChatResponse
            {
                Message = assistantMessageBuilder.ToString(),
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response");
            return new ChatResponse
            {
                Message = "I'm sorry, I encountered an error processing your request.",
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Updates multiple sleep tracking fields in a single atomic operation
    /// BEST PRACTICE: All fields updated together - either all succeed or all fail
    /// </summary>
    private async Task<string> UpdateSleepTrackingFieldsAsync(JsonElement args)
    {
        try
        {
            // Get today's entity
            var entity = await _tableStorageService.GetOrCreateTodayAsync();
            
            var updatedFields = new List<string>();
            
            // Iterate through all properties provided by the AI
            foreach (var property in args.EnumerateObject())
            {
                var fieldName = property.Name;
                var value = property.Value.GetString();
                
                if (string.IsNullOrEmpty(value))
                    continue;

                // Use reflection to set the property
                var propertyInfo = typeof(SleepTrackingEntity).GetProperty(fieldName);
                
                if (propertyInfo == null)
                {
                    _logger.LogWarning("Field '{Field}' does not exist, skipping", fieldName);
                    continue;
                }

                // Special handling for Notes field - append with bullet point
                if (fieldName == "Notes")
                {
                    var existingNotes = propertyInfo.GetValue(entity) as string;
                    if (string.IsNullOrEmpty(existingNotes) || existingNotes == "None")
                    {
                        // First note - just add the bullet point
                        propertyInfo.SetValue(entity, $"- {value}");
                    }
                    else
                    {
                        // Append new note with bullet point
                        propertyInfo.SetValue(entity, $"{existingNotes}\n- {value}");
                    }
                    updatedFields.Add($"{fieldName} appended: '- {value}'");
                }
                else
                {
                    propertyInfo.SetValue(entity, value);
                    updatedFields.Add($"{fieldName} = '{value}'");
                }
                
                _logger.LogInformation("Updated {Field} to {Value}", fieldName, value);
            }

            if (updatedFields.Count == 0)
            {
                return "No fields were updated.";
            }

            // Single atomic save of all changes
            await _tableStorageService.UpdateAsync(entity);

            var result = $"Successfully updated {updatedFields.Count} field(s): {string.Join(", ", updatedFields)}";
            _logger.LogInformation("Atomic update complete: {Result}", result);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sleep tracking fields");
            return $"Error updating fields: {ex.Message}";
        }
    }
}
