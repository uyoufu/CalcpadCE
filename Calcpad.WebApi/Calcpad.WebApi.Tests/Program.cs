using Calcpad.WebApi.Configs;
using Calcpad.WebApi.Services.AI;
using Calcpad.WebApi.Services.AI.Interface;
using Calcpad.WebApi.Tests.Services.Calcpad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var tests = new (string Name, Action Run)[]
{
    ("OpenAI provider creates an available OpenAI client", OpenAIProviderCreatesAvailableClient),
    ("iEPC provider creates an available iEPC client", IepcProviderCreatesAvailableClient),
    ("Unknown provider throws", UnknownProviderThrows),
    ("Invalid OpenAI provider configuration throws", InvalidOpenAIProviderConfigurationThrows),
    ("Invalid iEPC provider configuration throws", InvalidIepcProviderConfigurationThrows),
    ("Factory creates a new client instance for each create call", FactoryCreatesNewClientInstanceForEachCreateCall),
    (
        "SimplifyHtml preserves conditional chain when only else branch has inputs",
        CpdContentServiceTests.PreservesConditionalChainWhenOnlyElseBranchHasInputs
    ),
    (
        "SimplifyHtml preserves conditional skeleton when all branches are empty",
        CpdContentServiceTests.PreservesConditionalSkeletonWhenAllBranchesAreEmpty
    )
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS: {test.Name}");
}

static void OpenAIProviderCreatesAvailableClient()
{
    var services = CreateServices(
        new Dictionary<string, string?>
        {
            ["AI:Enable"] = "true",
            ["AI:Provider"] = "OpenAIChat",
            ["AI:OpenAIChat:Endpoint"] = "https://example.com/v1",
            ["AI:OpenAIChat:Model"] = "test-model",
            ["AI:OpenAIChat:ApiKey"] = "test-key",
            ["AI:OpenAIChat:MaxTokenLenght"] = "8192"
        }
    );

    IAIChatClient client = services.Factory.Create();

    AssertTrue(client.IsAvailable, "Client should be available.");
    AssertEqual(8192L, client.MaxTokenLength, "MaxTokenLength");
    AssertType<OpenAIChatClient>(client, "Client");
}

static void IepcProviderCreatesAvailableClient()
{
    var services = CreateServices(
        new Dictionary<string, string?>
        {
            ["AI:Enable"] = "true",
            ["AI:Provider"] = "IepcChat",
            ["AI:IepcChat:Endpoint"] = "http://localhost:7201",
            ["AI:IepcChat:ApiKey"] = "test-key",
            ["AI:IepcChat:WorkspaceName"] = "default",
            ["AI:IepcChat:MaxTokenLenght"] = "100000"
        }
    );

    IAIChatClient client = services.Factory.Create();

    AssertTrue(client.IsAvailable, "Client should be available.");
    AssertEqual(100000L, client.MaxTokenLength, "MaxTokenLength");
    AssertType<IepcChatClient>(client, "Client");
}

static void UnknownProviderThrows()
{
    var services = CreateServices(
        new Dictionary<string, string?>
        {
            ["AI:Enable"] = "true",
            ["AI:Provider"] = "UnknownProvider"
        }
    );

    var exception = AssertThrows<InvalidOperationException>(
        () => services.Factory.Create(),
        "Unknown provider should throw."
    );

    AssertContains("UnknownProvider", exception.Message, "Exception message");
}

static void InvalidOpenAIProviderConfigurationThrows()
{
    var services = CreateServices(
        new Dictionary<string, string?>
        {
            ["AI:Enable"] = "true",
            ["AI:Provider"] = "OpenAIChat",
            ["AI:OpenAIChat:Endpoint"] = "https://example.com/v1",
            ["AI:OpenAIChat:Model"] = "test-model",
            ["AI:OpenAIChat:MaxTokenLenght"] = "8192"
        }
    );

    var exception = AssertThrows<InvalidOperationException>(
        () => services.Factory.Create(),
        "Invalid OpenAI provider configuration should throw."
    );

    AssertContains("OpenAIChat", exception.Message, "Exception message");
}

static void InvalidIepcProviderConfigurationThrows()
{
    var services = CreateServices(
        new Dictionary<string, string?>
        {
            ["AI:Enable"] = "true",
            ["AI:Provider"] = "IepcChat",
            ["AI:IepcChat:Endpoint"] = "http://localhost:7201",
            ["AI:IepcChat:WorkspaceName"] = "default",
            ["AI:IepcChat:MaxTokenLenght"] = "100000"
        }
    );

    var exception = AssertThrows<InvalidOperationException>(
        () => services.Factory.Create(),
        "Invalid iEPC provider configuration should throw."
    );

    AssertContains("IepcChat", exception.Message, "Exception message");
}

static void FactoryCreatesNewClientInstanceForEachCreateCall()
{
    var services = CreateServices(
        new Dictionary<string, string?>
        {
            ["AI:Enable"] = "true",
            ["AI:Provider"] = "OpenAIChat",
            ["AI:OpenAIChat:Endpoint"] = "https://example.com/v1",
            ["AI:OpenAIChat:Model"] = "test-model",
            ["AI:OpenAIChat:ApiKey"] = "test-key",
            ["AI:OpenAIChat:MaxTokenLenght"] = "4096"
        }
    );

    var firstClient = services.Factory.Create();
    var secondClient = services.Factory.Create();

    AssertTrue(firstClient.IsAvailable, "First client should be available.");
    AssertTrue(secondClient.IsAvailable, "Second client should be available.");
    AssertNotSame(firstClient, secondClient, "Client");
}

static TestServices CreateServices(Dictionary<string, string?> configValues)
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(configValues)
        .Build();
    var appSettings = new AppSettings<AIConfig>(configuration);
    var factory = new AIChatClientFactory(
        appSettings,
        new TestHttpClientFactory(),
        NullLoggerFactory.Instance
    );

    return new TestServices(factory);
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static TException AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        return exception;
    }
    catch (Exception exception)
    {
        throw new InvalidOperationException(
            $"{message} Expected '{typeof(TException).Name}', got '{exception.GetType().Name}'."
        );
    }

    throw new InvalidOperationException($"{message} Expected '{typeof(TException).Name}'.");
}

static void AssertContains(string expected, string actual, string name)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"{name}: expected to contain '{expected}', got '{actual}'."
        );
    }
}

static void AssertNotSame(object? first, object? second, string name)
{
    if (ReferenceEquals(first, second))
    {
        throw new InvalidOperationException($"{name}: expected different object instances.");
    }
}

static void AssertType<T>(object? value, string name)
{
    if (value is not T)
    {
        throw new InvalidOperationException(
            $"{name}: expected type '{typeof(T).Name}', got '{value?.GetType().Name ?? "null"}'."
        );
    }
}

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected '{expected}', got '{actual}'.");
    }
}

sealed record TestServices(AIChatClientFactory Factory);

sealed class TestHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient();
    }
}
