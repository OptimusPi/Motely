namespace Motely.Filters.Ouija.State;

/// <summary>
/// Extended run state for Ouija filters.
/// Tracks owned jokers and consumed soul packs.
/// </summary>
public ref struct OuijaRunState
{
    // Base state
    public MotelyRunState BaseState;
    
    // Track which soul packs have been consumed
    // Key: (ante, packSlot) -> bool
    private ulong _consumedSoulPacks;
    
    public OuijaRunState()
    {
        BaseState = new MotelyRunState();
        _consumedSoulPacks = 0;
    }
    
    // Delegate to base state
    public bool ShowmanActive => BaseState.ShowmanActive;
    public MotelySingleItemSet OwnedJokers => BaseState.OwnedJokers;
    
    public void ActivateShowman() => BaseState.ActivateShowman();
    public void AddOwnedJoker(MotelyItem joker) => BaseState.AddOwnedJoker(joker);
    public bool CanObtainJoker(MotelyItem joker) => BaseState.CanObtainJoker(joker);
    
    public bool IsSoulPackConsumed(int ante, int packSlot)
    {
        // Each ante gets 8 bits (supports up to 8 pack slots per ante)
        int bitIndex = (ante - 1) * 8 + packSlot;
        if (bitIndex >= 64) return false;
        return (_consumedSoulPacks & (1UL << bitIndex)) != 0;
    }
    
    public void MarkSoulPackConsumed(int ante, int packSlot)
    {
        int bitIndex = (ante - 1) * 8 + packSlot;
        if (bitIndex < 64)
        {
            _consumedSoulPacks |= (1UL << bitIndex);
            DebugLogger.Log($"[OuijaRunState] Marked ante {ante} pack {packSlot} as consumed");
        }
    }
}