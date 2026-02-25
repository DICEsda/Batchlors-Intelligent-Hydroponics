using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class StartOtaRequestValidator : AbstractValidator<StartOtaRequest>
{
    public StartOtaRequestValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CoordId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.TargetVersion)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.RolloutPercentage)
            .InclusiveBetween(0, 100)
            .When(x => x.RolloutPercentage.HasValue);

        RuleFor(x => x.FailureThreshold)
            .InclusiveBetween(0, 100)
            .When(x => x.FailureThreshold.HasValue);
    }
}

public class CreateFirmwareRequestValidator : AbstractValidator<CreateFirmwareRequest>
{
    public CreateFirmwareRequestValidator()
    {
        RuleFor(x => x.Version)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.DownloadUrl)
            .NotEmpty()
            .MaximumLength(500);
    }
}
