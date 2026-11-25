namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlLessonPlanTests
{
    [MySqlFact]
    public Task Migration_privacy_replay_and_concurrent_edits_work_with_mysql()
        => LessonPlanHttpTests.VerifyAsync(Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"));
}
