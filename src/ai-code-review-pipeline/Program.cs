using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Net.Http.Headers;

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

    userRequest.Messages.Insert(0, new Message
    {
        Role = "system",
        Content = 
            "When user types 'typo check' or '1', you should replay '#1 typo checking' and review if code lines of currently opened file has typo"
    });
    
    userRequest.Messages.Insert(0, new Message
    {
        Role = "system",
        Content = 
            "When user types 'null check' or '2', you should reply '#2 null checking' and review if code lines of currently opened file handle null reference exception carefully"
    });

    userRequest.Messages.Insert(0, new Message
    {
        Role = "system",
        Content = 
            "When user types 'sdp' or 'sdl', you should reply 'reviewing with SDP' and review if code lines of currently opened file has typo first and then review handle null reference exception carefully"
    });

    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", githubToken);
    userRequest.Stream = true;

    var copilotLLMResponse = await httpClient.PostAsJsonAsync(
        githubCopilotCompletionsUrl, userRequest);

    var responseStream = 
        await copilotLLMResponse.Content.ReadAsStreamAsync();
    return Results.Stream(responseStream, "application/json");
});

app.MapGet("/callback", () => "You may close this tab and " + 
    "return to GitHub.com (where you should refresh the page " +
    "and start a fresh chat). If you're using VS Code or " +
    "Visual Studio, return there.");

app.Run();