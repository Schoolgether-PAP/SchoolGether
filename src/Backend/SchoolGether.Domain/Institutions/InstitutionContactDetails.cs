using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SchoolGether.Domain.Institutions;

public sealed record InstitutionContactDetails(string ContactEmail, string ContactRole, string? Phone,
    string? Address, string? PostalCode, string? TaxNumber, int ExpectedTeachers)
{
    public InstitutionContactDetails Normalize()
    {
        static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var value = this with { ContactEmail = ContactEmail?.Trim() ?? "", Phone = Optional(Phone),
            Address = Optional(Address), PostalCode = Optional(PostalCode), TaxNumber = Optional(TaxNumber) };
        if (value.ContactEmail.Length is < 1 or > 256 || !new EmailAddressAttribute().IsValid(value.ContactEmail) ||
            value.ContactRole is not ("Direction" or "IT" or "Secretary" or "Administration" or "Other") ||
            value.ExpectedTeachers is < 1 or > 10000 || value.Address?.Length > 250 ||
            (value.PostalCode is not null && !Regex.IsMatch(value.PostalCode, @"^[0-9]{4}-[0-9]{3}$")) ||
            (value.TaxNumber is not null && !Regex.IsMatch(value.TaxNumber, @"^[0-9]{9}$")) ||
            (value.Phone is not null && (value.Phone.Length > 25 || !Regex.IsMatch(value.Phone, @"^\+?[0-9 ()-]+$") || value.Phone.Count(char.IsAsciiDigit) is < 9 or > 15)))
            throw new ArgumentException("InvalidInstitutionContacts");
        return value;
    }
}
