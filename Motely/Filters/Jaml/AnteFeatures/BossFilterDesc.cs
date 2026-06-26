using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters.Jaml;

public sealed class BossClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public required MotelyBossBlind[] Bosses { get; set; }
    public int[] Rolls { get; set; } = [];
}

public readonly struct BossFilterDesc(BossClause clause)
    : IMotelySeedFilterDesc<BossFilterDesc.BossFilter>
{
    private readonly BossClause _clause = clause;

    public BossFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        int maxAnte = 0;
        for (int i = 0; i < _clause.Antes.Length; i++)
        {
            if (_clause.Antes[i] > maxAnte)
                maxAnte = _clause.Antes[i];
        }
        return new BossFilter(_clause, maxAnte);
    }

    public struct BossFilter(BossClause clause, int maxAnte) : IMotelySeedFilter
    {
        private readonly BossClause _clause = clause;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Bosses.Length > 0);

            var clause = _clause;
            int maxAnte = _maxAnte;

            return ctx.SearchIndividualSeeds(
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    var state = new MotelyRunState();

                    if (maxAnte > 0)
                    {
                        var cachedBosses = new MotelyBossBlind[maxAnte + 1];
                        var bossStream = singleCtx.CreateBossStream();
                        for (int ante = 1; ante <= maxAnte; ante++)
                            cachedBosses[ante] = singleCtx.GetBossForAnte(
                                ref bossStream,
                                ante,
                                ref state
                            );
                        state.CachedBosses = cachedBosses;
                    }

                    int totalCount = 0;
                    foreach (var ante in clause.Antes)
                    {
                        if (ante < 1 || ante > maxAnte)
                            continue;
                        bool found = false;
                        for (int i = 0; i < clause.Bosses.Length; i++)
                        {
                            if (clause.Bosses[i] == state.CachedBosses![ante])
                            {
                                found = true;
                                break;
                            }
                        }
                        if (found)
                            totalCount++;
                    }

                    return totalCount >= clause.Min;
                }
            );
        }
    }
}
