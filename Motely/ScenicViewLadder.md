
struct ladder(double state)
{
    state = 0.0;
    CurrentRung = 0;
    CurrentRung = climb(int steps);
}

[JSExport]
public ScenicViewDto GetNextScenicView(int feetHigh)
{
    double startingState = BlackBox.GetStartingState("ALEEB")
    var ladder = new Ladder(startingState)

    // this prints "Bushes, tree trunks"
    console.log(ladder.GetNextView())

    // burn call
    for (0..feetHigh-1) ladder.climb()

    
    // this prints "tree trunks, Birds, Power lines, Houses"
    console.log(ladder.GetNextView())


    // get the scenic view - using a DTO to appease Google Anti-Gravity's whining and bitching
    return new ScenicViewDto {
        view: ladder
    }
    
}



[JSExport]
public ScenicViewDto GetNextScenicView2(double state, int position)
{
    // state IS the rung. No burning. Just resume.
    var ladder = new Ladder(state)


    return new ScenicViewDto {
        view: ladder.GetNextView(),
        nextState: ladder.State   // ← pass back to JS. This IS the cursor.
    }
}