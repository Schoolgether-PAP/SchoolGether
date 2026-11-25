namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlLearningMaterialTests
{
    [MySqlFact]
    public Task Migration_isolation_publication_and_revocation_work_with_mysql()
        => LearningMaterialsHttpTests.VerifyAsync(Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"));
}
