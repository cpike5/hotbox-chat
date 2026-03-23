using FluentValidation;

namespace HotBox.Infrastructure.Validation;

public class CreateChannelRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateChannelValidator : AbstractValidator<CreateChannelRequest>
{
    public CreateChannelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Channel name is required.")
            .MaximumLength(100).WithMessage("Channel name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Channel description must not exceed 500 characters.");
    }
}
