using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class DosingRequestValidator : AbstractValidator<DosingRequest>
{
    public DosingRequestValidator()
    {
        RuleFor(x => x.NutrientAMl)
            .GreaterThanOrEqualTo(0)
            .When(x => x.NutrientAMl.HasValue);

        RuleFor(x => x.NutrientBMl)
            .GreaterThanOrEqualTo(0)
            .When(x => x.NutrientBMl.HasValue);

        RuleFor(x => x.PhUpMl)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PhUpMl.HasValue);

        RuleFor(x => x.PhDownMl)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PhDownMl.HasValue);

        // At least one mL amount must be specified
        RuleFor(x => x)
            .Must(x => x.NutrientAMl.HasValue
                    || x.NutrientBMl.HasValue
                    || x.PhUpMl.HasValue
                    || x.PhDownMl.HasValue)
            .WithMessage("At least one dosing amount must be specified.");
    }
}

public class ReservoirPumpRequestValidator : AbstractValidator<ReservoirPumpRequest>
{
    public ReservoirPumpRequestValidator()
    {
        RuleFor(x => x.DurationSeconds)
            .GreaterThan(0)
            .When(x => x.DurationSeconds.HasValue);
    }
}

public class ReservoirTargetsRequestValidator : AbstractValidator<ReservoirTargetsRequest>
{
    public ReservoirTargetsRequestValidator()
    {
        // pH range validation
        RuleFor(x => x.PhMin)
            .InclusiveBetween(0, 14)
            .When(x => x.PhMin.HasValue);

        RuleFor(x => x.PhMax)
            .InclusiveBetween(0, 14)
            .When(x => x.PhMax.HasValue);

        // EC range validation
        RuleFor(x => x.EcMin)
            .GreaterThanOrEqualTo(0)
            .When(x => x.EcMin.HasValue);

        RuleFor(x => x.EcMax)
            .GreaterThanOrEqualTo(0)
            .When(x => x.EcMax.HasValue);

        // Water temperature range validation
        RuleFor(x => x.TempMinC)
            .InclusiveBetween(-10, 50)
            .When(x => x.TempMinC.HasValue);

        RuleFor(x => x.TempMaxC)
            .InclusiveBetween(-10, 50)
            .When(x => x.TempMaxC.HasValue);

        // Cross-field: Min < Max for pH
        RuleFor(x => x.PhMin)
            .LessThan(x => x.PhMax!.Value)
            .When(x => x.PhMin.HasValue && x.PhMax.HasValue)
            .WithMessage("pH minimum must be less than pH maximum.");

        // Cross-field: Min < Max for EC
        RuleFor(x => x.EcMin)
            .LessThan(x => x.EcMax!.Value)
            .When(x => x.EcMin.HasValue && x.EcMax.HasValue)
            .WithMessage("EC minimum must be less than EC maximum.");

        // Cross-field: Min < Max for water temperature
        RuleFor(x => x.TempMinC)
            .LessThan(x => x.TempMaxC!.Value)
            .When(x => x.TempMinC.HasValue && x.TempMaxC.HasValue)
            .WithMessage("Water temperature minimum must be less than water temperature maximum.");
    }
}
