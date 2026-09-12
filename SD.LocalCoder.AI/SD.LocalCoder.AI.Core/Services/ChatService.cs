using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Model.Request.Chat;

namespace SD.LocalCoder.AI.Core.Services;

public sealed class ChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly IRepositoryContextService _repositoryContext;

    public ChatService(
        Kernel kernel,
        IRepositoryContextService repositoryContext)
    {
        _kernel = kernel;
        _repositoryContext = repositoryContext;
    }

    public async Task<(bool Success, string? Response, string? Error, IReadOnlyList<string>? IncludedPaths)> ChatAsync(
        ChatWithRepoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Prompt))
            return (false, null, "Prompt is required", null);

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            history.AddSystemMessage(BuildSystemMessage());

            IReadOnlyList<string> includedPaths = [];

            if (!string.IsNullOrWhiteSpace(request.RepoId))
            {
                var context = await _repositoryContext.BuildContextAsync(
                    request.RepoId,
                    request.Prompt,
                    request.Paths,
                    cancellationToken);

                if (context is not null)
                {
                    includedPaths = context.IncludedPaths;

                    history.AddUserMessage(BuildRepositoryAwareMessage(
                        request.Prompt,
                        context));
                }
                else
                {
                    history.AddUserMessage(
                        $"Repository '{request.RepoId}' could not be analyzed. " +
                        "Answer only from the user's task and clearly state when repository-specific information is unavailable.\n\n" +
                        request.Prompt);
                }
            }
            else
            {
                history.AddUserMessage(request.Prompt);
            }

            var result = await chatService.GetChatMessageContentAsync(
                history,
                cancellationToken: cancellationToken);

            return (true, result.Content, null, includedPaths);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message, null);
        }
    }

    private static string BuildSystemMessage() =>
        """
        You are SD LocalCoder AI, a repository-aware software engineering agent.

        Your job is to understand the user's task and work WITH the existing codebase,
        not invent a new architecture unnecessarily.

        When repository context is supplied:
        1. Treat existing code as the source of truth.
        2. Reuse existing classes, interfaces, services, DTOs, helpers and patterns.
        3. Follow the discovered architecture and folder structure.
        4. Do not duplicate functionality that already exists.
        5. Keep changes narrowly scoped to the requested task.
        6. If the requested feature should extend an existing pattern, explicitly identify that pattern.
        7. Do not claim that a file was changed or created unless you provide the proposed change.
        8. For implementation requests, identify affected files and provide complete code where practical.
        9. Never invent APIs, classes or dependencies when repository context already provides an equivalent.
        10. Prefer maintainable, testable and dependency-injected code.
        """;

    private static string BuildRepositoryAwareMessage(
        string prompt,
        SD.LocalCoder.AI.Model.Response.Repository.RepositoryContextResult context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== REPOSITORY INTELLIGENCE ===");

        if (context.Architecture is not null)
        {
            sb.AppendLine($"Primary language: {context.Architecture.PrimaryLanguage}");
            sb.AppendLine($"Architecture: {context.Architecture.Architecture}");

            if (context.Architecture.Frameworks.Count > 0)
                sb.AppendLine($"Frameworks: {string.Join(", ", context.Architecture.Frameworks)}");

            if (context.Architecture.Layers.Count > 0)
                sb.AppendLine($"Layers: {string.Join(", ", context.Architecture.Layers)}");

            if (context.Architecture.Patterns.Count > 0)
                sb.AppendLine($"Patterns: {string.Join(", ", context.Architecture.Patterns)}");

            foreach (var rule in context.Architecture.Rules)
                sb.AppendLine($"Rule: {rule}");
        }

        if (context.RelatedSymbols.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== RELATED EXISTING CODE ===");

            foreach (var item in context.RelatedSymbols.Take(20))
            {
                sb.AppendLine(
                    $"- {item.Kind} {item.Name} | {item.Path} | " +
                    $"Role={item.ArchitectureRole} | Score={item.Score}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== SELECTED SOURCE FILES ===");
        sb.AppendLine(context.Context);

        sb.AppendLine("=== USER TASK ===");
        sb.AppendLine(prompt);

        sb.AppendLine();
        sb.AppendLine(
            "Before producing implementation details, reason from the selected existing code. " +
            "Prefer an existing similar implementation whenever one is available.");

        return sb.ToString();
    }
}
