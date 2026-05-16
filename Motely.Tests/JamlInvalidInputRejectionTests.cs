using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// Pins JAML inputs that are *deliberately* rejected at parse time — the assertion is
/// "TryLoad returns false with a non-empty error", not "TryLoad succeeds". Lives separately
/// from V0FilterRegressionTests (which auto-discovers filters/*.jaml and asserts they parse).
public sealed class JamlInvalidInputRejectionTests
{
    [Fact]
    public void CatchesInvalidVoucher_AnyInShouldClause_ParseFails()
    {
        var jaml = """
            name: invalid-voucher-any-in-should
            deck: Red
            stake: White
            should:
              - voucher: Any
                antes: [1]
                score: 1
            """;

        bool parsed = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.False(parsed, "voucher:Any inside should: should not parse — it is a no-op match that always contributes its score to every seed, so it has no scoring value.");
        Assert.Null(config);
        Assert.False(string.IsNullOrWhiteSpace(error), "rejection must produce a non-empty error message");
    }
}
