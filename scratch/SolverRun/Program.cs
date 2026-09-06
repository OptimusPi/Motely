using Motely;
using Motely.Analysis;

// usage: SolverRun <seed> <item> <firstAnte> <lastAnte> [deck] [stake]
string seed = args.Length > 0 ? args[0] : "OMOANV53";
string item = args.Length > 1 ? args[1] : "Showman";
int first = args.Length > 2 ? int.Parse(args[2]) : 1;
int last = args.Length > 3 ? int.Parse(args[3]) : 8;
var deck = args.Length > 4 ? Enum.Parse<MotelyDeck>(args[4], true) : MotelyDeck.Red;
var stake = args.Length > 5 ? Enum.Parse<MotelyStake>(args[5], true) : MotelyStake.White;

Console.WriteLine($"{seed}  {item}  antes {first}-{last}  {deck}/{stake}");
var paths = MotelyItemSolver.Solve(seed, item, first, last, deck, stake);
Console.Write(MotelyItemSolver.Format(paths));
