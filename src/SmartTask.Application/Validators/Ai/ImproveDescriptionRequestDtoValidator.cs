using FluentValidation;
using SmartTask.Application.DTOs.Ai;
using SmartTask.Application.Validators.Common;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.Validators.Ai;

/// <summary>
/// Rules for <c>POST ai/task-descriptions/improve</c>. The length limits also cap how much text, and
/// so how many tokens, a client can send to the AI provider.
/// </summary>
public sealed class ImproveDescriptionRequestDtoValidator : AbstractValidator<ImproveDescriptionRequestDto>
{
    public ImproveDescriptionRequestDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Description)
            .NotBlank()
            .TrimmedLength(AiConstraints.InputMinLength, AiConstraints.InputMaxLength);

        RuleFor(x => x.Title)
            .TrimmedMaxLength(TaskItemConstraints.TitleMaxLength);
    }
}
