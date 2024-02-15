using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OaiFuncCall.Models;

namespace OaiFuncCall.Pages;

public class IndexModel : PageModel {
  private readonly ILogger<IndexModel> _logger;
  private readonly IConfiguration _config;

  [BindProperty]
  public string? Reply { get; set; }

  public IndexModel(ILogger<IndexModel> logger, IConfiguration config) {
    _logger = logger;
    _config = config;
  }

  public void OnGet() { }

  // action method that receives prompt from the form
  public async Task<IActionResult> OnPostAsync(string prompt) {
    // call the Azure Function
    var response = await CallFunction(prompt);
    Reply = response;
    return Page();
  }

  private async Task<string> CallFunction(string question) {
    string endpoint = _config["AzureOpenAiSettings:Endpoint"]!;
    string apiKey = _config["AzureOpenAiSettings:ApiKey"]!;
    string model = _config["AzureOpenAiSettings:Model"]!;

    Uri openAIUri = new(endpoint);

    // Instantiate OpenAIClient for Azure Open AI.
    OpenAIClient client = new(openAIUri, new AzureKeyCredential(apiKey));

    ChatCompletionsOptions chatCompletionsOptions = new();
    chatCompletionsOptions.DeploymentName = model;

    ChatChoice responseChoice;
    Response<ChatCompletions> responseWithoutStream;

    // Add function definitions
    FunctionDefinition getProductFunctionDefinition = ProductAgent.GetFunctionDefinition();
    FunctionDefinition getMostExpensiveProductDefinition = MostExpensiveProductAgent.GetFunctionDefinition();

    chatCompletionsOptions.Functions.Add(getProductFunctionDefinition);
    chatCompletionsOptions.Functions.Add(getMostExpensiveProductDefinition);


    chatCompletionsOptions.Messages.Add(
        new ChatRequestUserMessage(question)
    );

    responseWithoutStream =
        await client.GetChatCompletionsAsync(chatCompletionsOptions);

    responseChoice = responseWithoutStream.Value.Choices[0];

    while (responseChoice.FinishReason!.Value == CompletionsFinishReason.FunctionCall) {
      // Add message as a history.
      chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(responseChoice.Message.ToString()));

      if (responseChoice.Message.FunctionCall.Name == ProductAgent.Name) {
        string unvalidatedArguments = responseChoice.Message.FunctionCall.Arguments;
        ProductInput input = JsonSerializer.Deserialize<ProductInput>(unvalidatedArguments,
          new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;

        var functionResultData = ProductAgent.GetProductDetails(input.ProductName);

        var functionResponseMessage = new ChatRequestFunctionMessage(
          ProductAgent.Name,
          JsonSerializer.Serialize(
            functionResultData,
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        chatCompletionsOptions.Messages.Add(functionResponseMessage);
      } else if (responseChoice.Message.FunctionCall.Name == MostExpensiveProductAgent.Name) {
        string unvalidatedArguments = responseChoice.Message.FunctionCall.Arguments;
        var functionResultData = MostExpensiveProductAgent.GetMostExpensiveProductDetails();
        var functionResponseMessage = new ChatRequestFunctionMessage(
          MostExpensiveProductAgent.Name,
          JsonSerializer.Serialize(
            functionResultData,
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        chatCompletionsOptions.Messages.Add(functionResponseMessage);
      }


      // Call LLM again to generate the response.
      responseWithoutStream = await client.GetChatCompletionsAsync(chatCompletionsOptions);

      responseChoice = responseWithoutStream.Value.Choices[0];
    }

    return responseChoice.Message.Content;
  }
}

