namespace SchoolGether.Application.Identity;

// Apenas códigos: as descrições do Identity podem incluir dados introduzidos.
public sealed class BootstrapValidationException(IReadOnlyList<string> errorCodes) : Exception("BootstrapValidationFailed")
{
    public IReadOnlyList<string> ErrorCodes { get; } = errorCodes;
}
