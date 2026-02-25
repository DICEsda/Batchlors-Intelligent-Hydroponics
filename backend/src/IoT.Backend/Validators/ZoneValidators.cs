using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class CreateZoneRequestValidator : AbstractValidator<CreateZoneRequest>
{
    public CreateZoneRequestValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.CoordinatorId)
            .NotEmpty()
            .MaximumLength(100);
    }
}
