using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello Copilot!");

// make sure you change the App Name below
string yourGitHubAppName = "AICodeReviewPipeline";
string githubCopilotCompletionsUrl = 
    "https://api.githubcopilot.com/chat/completions";

app.MapPost("/agent", async (
    [FromHeader(Name = "X-GitHub-Token")] string githubToken, 
    [FromBody] Request userRequest) =>
{
    var octokitClient = 
        new GitHubClient(
            new Octokit.ProductHeaderValue(yourGitHubAppName))
    {
        Credentials = new Credentials(githubToken)
    };
    var user = await octokitClient.User.Current();

    userRequest.Messages.Insert(0, new Message
    {
        Role = "system",
        Content = 
            "Start every response with the user's name, " + 
            $"which is @{user.Login}"
    });
    userRequest.Messages.Insert(0, new Message
    {
        Role = "system",
        Content = 
            "You are a helpful assistant that replies to " +
            "user messages as if you were one code reviewer."
    });

    Dictionary<string, string> userPipelineMap = new Dictionary<string, string>
    {
        { "1. typo check", "Pls check if currently opened file has typo" },
        { "2. null check", "Pls check if currently opened file handles null reference exception carefully" },
        { "3. Fallback logic check", "Pls check fallback logic in currently opened file to see if there is any potential issue" },
        { "4. Config check", "Pls check if there is any potential issue for currently opened file if it is config file" }
    };

    Dictionary<string, string> resultMap = new Dictionary<string, string>
    {
        { "1. typo check", "TODO" },
        { "2. null check", "TODO" },
        { "3. Fallback logic check", "TODO" },
        { "4. Config check", "TODO" }
    };
    
    foreach (var userPipeline in userPipelineMap)
    {
        userRequest.Messages.Insert(0, new Message
        {
            Role = "user",
            Content = userPipeline.Value
        });

        // get result
        var rawHttpClient = new HttpClient();
        rawHttpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", githubToken);
        userRequest.Stream = true;

        var rawCopilotLLMResponse = await rawHttpClient.PostAsJsonAsync(
            githubCopilotCompletionsUrl, userRequest);

        if (rawCopilotLLMResponse.IsSuccessStatusCode)
        {
            string rawResponseString = 
                await rawCopilotLLMResponse.Content.ReadAsStringAsync();
            resultMap[userPipeline.Key] = rawResponseString;
        }
        else
        {
            resultMap[userPipeline.Key] = "Error: Unable to get response";
        }
    }

    string rawResult = JsonConvert.SerializeObject(resultMap);
    userRequest.Messages.Insert(0, new Message
    {
        Role = "user",
        Content = 
            "Pls summarize the review results in a table format."
            + "\n" + rawResult
    });

    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", githubToken);
    userRequest.Stream = true;

    var copilotLLMResponse = await httpClient.PostAsJsonAsync(
        githubCopilotCompletionsUrl, userRequest);

    var responseStreamString = 
        await copilotLLMResponse.Content.ReadAsStringAsync();
    
    // Raw result + Summarized result
    string fullResult = rawResult + responseStreamString;
    Stream fullResultStream = new MemoryStream(Encoding.UTF8.GetBytes(fullResult));

    return Results.Stream(fullResultStream, "application/json");
});

app.MapGet("/callback", () => "You may close this tab and " + 
    "return to GitHub.com (where you should refresh the page " +
    "and start a fresh chat). If you're using VS Code or " +
    "Visual Studio, return there.");

app.Run();