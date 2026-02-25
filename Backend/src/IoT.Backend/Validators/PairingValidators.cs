using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class StartPairingRequestValidator : AbstractValidator<StartPairingRequest>
{
    public StartPairingRequestValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CoordId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DurationSeconds)
            .InclusiveBetween(10, 300);
    }
}

public class StopPairingRequestValidator : AbstractValidator<StopPairingRequest>
{
    public StopPairingRequestValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CoordId)
            .NotEmpty()
            .MaximumLength(100);
    }
}

public class ApproveTowerRequestValidator : AbstractValidator<ApproveTowerRequest>
{
    public ApproveTowerRequestValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CoordId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.TowerId)
            .NotEmpty()
            .MaximumLength(100);
    }
}

public class RejectTowerRequestValidator : AbstractValidator<RejectTowerRequest>
{
    public RejectTowerRequestValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CoordId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.TowerId)
            .NotEmpty()
            .MaximumLength(100);
    }
}

public class ForgetDeviceRequestValidator : AbstractValidator<ForgetDeviceRequest>
{
    public ForgetDeviceRequestValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CoordId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.TowerId)
            .NotEmpty()
            .MaximumLength(100);
    }
}
