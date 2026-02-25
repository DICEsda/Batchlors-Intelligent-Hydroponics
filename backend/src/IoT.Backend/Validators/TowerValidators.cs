using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class UpdateNameRequestValidator : AbstractValidator<UpdateNameRequest>
{
    public UpdateNameRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}

public class SetLightRequestValidator : AbstractValidator<SetLightRequest>
{
    public SetLightRequestValidator()
    {
        RuleFor(x => x.Brightness)
            .InclusiveBetween(0, 100)
            .When(x => x.Brightness.HasValue);
    }
}

public class SetPumpRequestValidator : AbstractValidator<SetPumpRequest>
{
    public SetPumpRequestValidator()
    {
        RuleFor(x => x.DurationSeconds)
            .GreaterThan(0)
            .When(x => x.DurationSeconds.HasValue);
    }
}

public class RecordHeightRequestValidator : AbstractValidator<RecordHeightRequest>
{
    public RecordHeightRequestValidator()
    {
        RuleFor(x => x.SlotIndex)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.HeightCm)
            .GreaterThan(0)
            .LessThanOrEqualTo(300);

        RuleFor(x => x.Method)
            .IsInEnum();
    }
}

public class SetCropRequestValidator : AbstractValidator<SetCropRequest>
{
    public SetCropRequestValidator()
    {
        RuleFor(x => x.CropType)
            .IsInEnum();
    }
}

public class LedPreviewRequestValidator : AbstractValidator<LedPreviewRequest>
{
    public LedPreviewRequestValidator()
    {
        RuleFor(x => x.Brightness)
            .InclusiveBetween(0, 100);

        RuleFor(x => x.Duration)
            .InclusiveBetween(1, 60);
    }
}
