using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-500: a link can open the supervisor queue on one kind of work; an unknown or blank kind opens it all.
public class SupervisorQueueKindQueryTests
{
    [Fact]
    public void KindFromQuery_MatchesAKnownKind_AnyCase()
    {
        Assert.Equal(SupervisorQueueRule.KindOverrideReview, SupervisorQueueRule.KindFromQuery("overridereview"));
        Assert.Equal(SupervisorQueueRule.KindResultVerify, SupervisorQueueRule.KindFromQuery(" ResultPendingVerification "));
        Assert.Null(SupervisorQueueRule.KindFromQuery("Something"));
        Assert.Null(SupervisorQueueRule.KindFromQuery(""));
        Assert.Null(SupervisorQueueRule.KindFromQuery(null));
        // Cycle 700 (OCD-083): the reported-transfusion kind is the sixth, linkable like the rest.
        Assert.Equal(6, SupervisorQueueRule.Kinds.Distinct().Count());
        Assert.Equal(SupervisorQueueRule.KindTransfusionReported, SupervisorQueueRule.KindFromQuery("transfusionreported"));
    }
}
