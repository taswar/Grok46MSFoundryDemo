using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAIChatClient = OpenAI.Chat.ChatClient;

#pragma warning disable OPENAI001

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();
 
var deploymentName = config["AZURE_OPENAI_DEPLOYMENT"] ?? "grok-4.6";
var endpoint = config["AZURE_AI_ENDPOINT"]
    ?? throw new InvalidOperationException(
        "AZURE_AI_ENDPOINT is not set. Run: dotnet user-secrets set \"AZURE_AI_ENDPOINT\" \"<your-endpoint-e.g.-https://<resource>.services.ai.azure.com/models>\"");

// Grok is a partner (MaaS) model, not a native Azure OpenAI deployment, so it can't be
// reached through AzureOpenAIClient's /openai/deployments/... path. Use the generic
// OpenAI ChatClient against the Foundry model endpoint instead (mirrors Grok4.3 sample).
BearerTokenPolicy tokenPolicy = new(
    new DefaultAzureCredential(),
    "https://ai.azure.com/.default");

var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
clientOptions.AddPolicy(new ApiVersionPolicy("2024-05-01-preview"), PipelinePosition.PerCall);

OpenAIChatClient openAiChatClient = new(
    model: deploymentName,
    authenticationPolicy: tokenPolicy,
    options: clientOptions);

IChatClient chatClient = openAiChatClient
    .AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var chatOptions = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(GetServiceHealth),
        AIFunctionFactory.Create(GetRecentErrorRate)
    ],
    // Reasoning effort tuned for a multi-step decision — not maximum, not minimal.
    AdditionalProperties = new AdditionalPropertiesDictionary
    {
        ["reasoning_effort"] = "high" // low | medium | high | xhigh
    }
};

var messages = new List<ChatMessage>
{
    new(ChatRole.System,
        "You are a deployment safety agent. Before recommending a deploy, check both " +
        "service health and recent error rate. Reason about both signals together before " +
        "concluding — do not approve a deploy based on a single check alone."),
    new(ChatRole.User, "Is it safe to deploy service 'checkout-api' right now?")
};
 
var response = await chatClient.GetResponseAsync(messages, chatOptions);
Console.WriteLine(response.Text);
 
Console.WriteLine("\nPress ENTER to run Multi-Stage Code Review Across a Repository");
Console.ReadLine();
Console.WriteLine("\n**********************Case 2******************************\n");

 
var interfaceFile = """
    public interface IOrderRepository
    {
        Task<Order> GetByIdAsync(Guid orderId);
        Task SaveAsync(Order order);
    }
    """;
 
var implementationFile = """
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _db;
        public OrderRepository(AppDbContext db) => _db = db;
 
        public async Task<Order> GetByIdAsync(Guid orderId)
            => await _db.Orders.FindAsync(orderId);
 
        public async Task SaveAsync(Order order)
        {
            _db.Orders.Update(order);
            await _db.SaveChangesAsync();
        }
    }
    """;
 
var callerFile = """
    public class OrderService
    {
        private readonly IOrderRepository _repo;
        public OrderService(IOrderRepository repo) => _repo = repo;
 
        public async Task CancelOrderAsync(Guid orderId)
        {
            var order = await _repo.GetByIdAsync(orderId);
            order.Status = OrderStatus.Cancelled; // no null check
            await _repo.SaveAsync(order);
        }
    }
    """;
 
var systemPrompt = """
    You are a senior .NET code reviewer. You'll be given multiple related files from
    the same change set. Review them together, not in isolation — flag issues that only
    become visible when you consider how the files interact (e.g. a nullable return type
    being used without a null check downstream). Be direct and specific about severity.
    """;
 
var messagesMulti = new List<ChatMessage>
{
    new(ChatRole.System, systemPrompt),
    new(ChatRole.User, $"""
        Review this change set:
 
        --- IOrderRepository.cs ---
        {interfaceFile}
 
        --- OrderRepository.cs ---
        {implementationFile}
 
        --- OrderService.cs ---
        {callerFile}
        """)
};
 
var responseMulti = await chatClient.GetResponseAsync(messagesMulti);
Console.WriteLine(responseMulti.Text);

Console.WriteLine("\nPress ENTER to run Research and Analysis — Structured, Decision-Ready Output");
Console.ReadLine();
Console.WriteLine("\n**********************Case 3******************************\n");

var sourceMaterial = """
    Q3 vendor evaluation notes:
    Vendor A: $42/user/month, 99.95% uptime SLA, no SOC 2 report available yet,
    onboarding takes ~6 weeks, strong API docs.
    Vendor B: $58/user/month, 99.99% uptime SLA, SOC 2 Type II certified,
    onboarding takes ~2 weeks, API docs are sparse.
    """;
 
var messagesRA = new List<ChatMessage>
{
    new(ChatRole.System,
        "Synthesize the provided source material into a structured, decision-ready " +
        "recommendation. Output valid JSON matching the requested schema only."),
    new(ChatRole.User, $"Analyze this vendor comparison and recommend one:\n\n{sourceMaterial}")
};
 
var chatOptionsRA = new ChatOptions
{
    ResponseFormat = ChatResponseFormat.ForJsonSchema<VendorRecommendation>() 
};
 
var responseRA = await chatClient.GetResponseAsync(messagesRA, chatOptionsRA);
var recommendation = JsonSerializer.Deserialize<VendorRecommendation>(responseRA.Text);
 
Console.WriteLine($"Recommended: {recommendation?.RecommendedVendor}");
Console.WriteLine($"Reasoning: {recommendation?.Reasoning}");
Console.WriteLine($"Key risk: {recommendation?.KeyRisk}");
 
Console.WriteLine("\nPress Enter to run Multi-Modal Input — Image + Text");
Console.ReadLine();
Console.WriteLine("\n*********************Case 4 *******************************\n");

var diagramBytes = await File.ReadAllBytesAsync("architecture-diagram-spof.png");
 
var messageImage = new ChatMessage(ChatRole.User,
[
    new TextContent("Review this architecture diagram. Flag any single points of failure."),
    new DataContent(diagramBytes, "image/png")
]);
 
var responseImage = await chatClient.GetResponseAsync([messageImage]);
Console.WriteLine(responseImage.Text);

// --- Tool stand-ins for a real monitoring/ops API ---

[Description("Gets the current health status of a named service.")]
static string GetServiceHealth(
    [Description("The service name, e.g. checkout-api")] string serviceName)
{
    return serviceName switch
    {
        "checkout-api" => "Healthy. All instances passing readiness checks.",
        _ => "Unknown service."
    };
}
 
[Description("Gets the error rate for a named service over the last hour.")]
static string GetRecentErrorRate(
    [Description("The service name, e.g. checkout-api")] string serviceName)
{
    return serviceName switch
    {
        "checkout-api" => "Error rate: 4.2% over the last hour (baseline: 0.3%). Elevated.",
        _ => "No data available."
    };
}

record VendorRecommendation(
    [property: JsonPropertyName("recommended_vendor")] string RecommendedVendor,
    [property: JsonPropertyName("reasoning")] string Reasoning,
    [property: JsonPropertyName("key_risk")] string KeyRisk
);


/// <summary>Pipeline policy that appends api-version as a query parameter to every request.</summary>
class ApiVersionPolicy(string apiVersion) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        AppendApiVersion(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        AppendApiVersion(message);
        await ProcessNextAsync(message, pipeline, currentIndex);
    }

    private void AppendApiVersion(PipelineMessage message)
    {
        var url = message.Request.Uri!.ToString();
        var separator = url.Contains('?') ? "&" : "?";
        message.Request.Uri = new Uri($"{url}{separator}api-version={apiVersion}");
    }
}