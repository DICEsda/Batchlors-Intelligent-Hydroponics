using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class WifiConfigRequestValidator : AbstractValidator<WifiConfigRequest>
{
    public WifiConfigRequestValidator()
    {
        RuleFor(x => x.Ssid)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(64);
    }
}
