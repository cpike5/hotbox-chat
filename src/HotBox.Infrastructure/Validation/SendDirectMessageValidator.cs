using FluentValidation;

namespace HotBox.Infrastructure.Validation;

public class SendDirectMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public class SendDirectMessageValidator : AbstractValidator<SendDirectMessageRequest>
{
    public SendDirectMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(4000).WithMessage("Message content must not exceed 4000 characters.");
    }
}
