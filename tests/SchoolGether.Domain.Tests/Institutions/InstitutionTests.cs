using SchoolGether.Domain.Institutions;

namespace SchoolGether.Domain.Tests.Institutions;

public sealed class InstitutionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Institution(name!));
    }

    [Fact]
    public void Constructor_RejectsNameLongerThanDatabaseLimit()
    {
        Assert.Throws<ArgumentException>(() => new Institution(new string('a', 201)));
    }

    [Fact]
    public void Constructor_AcceptsAccentsAndTrimsSurroundingSpaces()
    {
        var institution = new Institution("  Instituto Politécnico  ", 7);

        Assert.Equal("Instituto Politécnico", institution.Name);
        Assert.Equal(7, institution.CreatedBy);
        Assert.Equal(DateTimeKind.Utc, institution.CreatedAt.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsInvalidCreatorId(int createdBy)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Institution("Instituto", createdBy));
    }

    [Fact]
    public void Rename_AdvancesVersionAndPreservesCreationAudit()
    {
        var institution = new Institution("Instituto", 7);
        var createdAt = institution.CreatedAt;
        var initialVersion = institution.RowVersion;

        institution.Rename("Instituto Politécnico");

        Assert.Equal("Instituto Politécnico", institution.Name);
        Assert.Equal(initialVersion + 1, institution.RowVersion);
        Assert.NotNull(institution.UpdatedAt);
        Assert.Equal(createdAt, institution.CreatedAt);
        Assert.Equal(7, institution.CreatedBy);
    }

    [Fact]
    public void Rename_InvalidNameLeavesEntityUnchanged()
    {
        var institution = new Institution("Instituto");
        var initialVersion = institution.RowVersion;

        Assert.ThrowsAny<ArgumentException>(() => institution.Rename(" "));

        Assert.Equal("Instituto", institution.Name);
        Assert.Equal(initialVersion, institution.RowVersion);
        Assert.Null(institution.UpdatedAt);
    }

    [Fact]
    public void Rename_SameNameDoesNotCreateAnUpdate()
    {
        var institution = new Institution("Instituto");
        var initialVersion = institution.RowVersion;

        institution.Rename(" Instituto ");

        Assert.Equal(initialVersion, institution.RowVersion);
        Assert.Null(institution.UpdatedAt);
    }

    [Fact]
    public void Delete_IsIdempotentAndPreventsLaterRenaming()
    {
        var institution = new Institution("Instituto");
        var initialVersion = institution.RowVersion;

        institution.Delete();
        var deletedAt = institution.DeletedAt;
        institution.Delete();

        Assert.NotNull(deletedAt);
        Assert.Equal(deletedAt, institution.DeletedAt);
        Assert.Equal(initialVersion + 1, institution.RowVersion);
        Assert.Throws<InvalidOperationException>(() => institution.Rename("Outro instituto"));
    }
}
