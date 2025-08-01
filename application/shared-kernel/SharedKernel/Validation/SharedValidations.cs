using FluentValidation;

namespace PlatformPlatform.SharedKernel.Validation;

public static class SharedValidations
{
    public sealed class Email : AbstractValidator<string>
    {
        // While emails can be longer, we will limit them to 100 characters which should be enough for most cases
        private const int EmailMaxLength = 100;

        public Email(bool allowEmpty = false)
        {
            const string errorMessage = "Email must be in a valid format and no longer than 100 characters.";

            var rule = RuleFor(email => email)
                .EmailAddress()
                .WithMessage(errorMessage)
                .MaximumLength(EmailMaxLength)
                .WithMessage(errorMessage)
                .Must(email => email == email.ToLowerInvariant())
                .WithMessage(errorMessage)
                .Must(email => email == email.Trim())
                .WithMessage(errorMessage)
                .Must(email => !email.Contains(".."))
                .WithMessage(errorMessage)
                .Must(email =>
                    {
                        var parts = email.Split('@');
                        return parts.Length == 2 &&
                               !parts[0].StartsWith('.') &&
                               !parts[0].EndsWith('.') &&
                               !parts[1].StartsWith('.') &&
                               !parts[1].EndsWith('.');
                    }
                )
                .WithMessage(errorMessage);

            if (!allowEmpty)
            {
                rule.NotEmpty().WithMessage(errorMessage);
            }
        }
    }

    public sealed class Phone : AbstractValidator<string?>
    {
        // The ITU-T E.164 standard limits phone numbers to 15 digits (including country code),
        // Additional 5 characters are added to allow for spaces, dashes, parentheses, etc.
        private const int PhoneMaxLength = 20;

        public Phone(string phoneName = nameof(Phone))
        {
            const string errorMessage = "Phone must be in a valid format and no longer than 20 characters.";
            RuleFor(phone => phone)
                .MaximumLength(PhoneMaxLength)
                .WithName(phoneName)
                .WithMessage(errorMessage)
                .Matches(@"^\+?(\d[\d-. ]+)?(\([\d-. ]+\))?[\d-. ]+\d$")
                .WithMessage(errorMessage)
                .When(phone => !string.IsNullOrEmpty(phone));
        }
    }
}
