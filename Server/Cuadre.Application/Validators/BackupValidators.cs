using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Backups: shape rules only. Whether the folder exists and can be written is checked by BackupService (422).

public class BackupSettingsValidator : AbstractValidator<BackupSettings>
{
    public BackupSettingsValidator()
    {
        RuleFor(x => x.AlertAfterDays)
            .InclusiveBetween(1, 60).WithMessage("El aviso va de 1 a 60 días.");

        RuleFor(x => x.ExternalDirectory)
            .MaximumLength(240).WithMessage("La ruta es demasiado larga.")
            .Must(path => Path.IsPathFullyQualified(path!.Trim()))
            .WithMessage("Escribí la ruta completa, con la letra de la unidad (por ejemplo E:\\Cuadre respaldos).")
            .When(x => !string.IsNullOrWhiteSpace(x.ExternalDirectory));
    }
}
