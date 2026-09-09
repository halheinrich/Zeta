# Zeta

> Collaboration contract → `../AGENTS.md`.
> Cross-cutting status & dependency graph → `../INSTRUCTIONS.md`.
> Mission, principles & repo conventions → `../VISION.md`.

The deep working reference for this submodule. Ratified design for the
investigation it serves → `../SPEC-rational-ratio.md`.

## Stack

A C# class library and its xUnit test project; language version, target
framework and namespace conventions are umbrella-wide and live in
`../VISION.md` and `Directory.Build.props`.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\Math\Zeta\Zeta.slnx`

## Repo

`https://github.com/halheinrich/Zeta`, branch `main`. Public from its first
commit, which is why the conjecture disclaimer lives in `README.md` rather
than being added when results appear.

## Depends on

- **RealConstants** — the `IRealConstant` providers this pipeline composes:
  π, ζ(s) and the square roots, each proving its own truncation bound. This
  repository defines no constant and must not start.

  By `ProjectReference`, per the umbrella's ruling on intra-umbrella edges, at
  `..\..\RealConstants\RealConstants\RealConstants.csproj`. That path
  **escapes this repository** — see § Pitfalls.

- **RationalApproximation** — every contract wired here. `Approximation` and
  its arithmetic, `IRealConstant`, `IRationalApproximator` with the reference
  `DenominatorSweep`, `RationalCandidate`, and `TrendIteration` /
  `TrendRow` / `TrendMatrix`.

- **BigRationalLibrary** — `HalHeinrich.Numerics.BigRational`, the exact
  rational every value, bound and matrix cell is computed in.

  Both are reached **transitively**, through the single reference above.
  Measured rather than inferred from the reference graph: a throwaway file
  naming `BigRational`, `Approximation.Create`, `IRealConstant` and `MachinPi`
  in one method was compiled here before a second reference was considered,
  and it resolved. So a checkout needs all four repositories as siblings even
  though only one reference is written here — the longest such chain in the
  umbrella, and the cost of being the member that composes rather than
  provides.

## Layout

- **`Zeta`** — the library. The composition of the method in
  `../SPEC-rational-ratio.md` § 2, and nothing else: it introduces no
  enclosure, no search and no constant of its own.
- **`Zeta.Tests`** — xUnit. Also holds `StubConstant`, a provider of nothing
  whose enclosures are written out by the test that needs them. It is
  scaffolding rather than a fixture: the pipeline has decisions that no real
  provider's numbers reach, and a stub is the only way to hand it the numbers
  that do.
- **`Zeta.Experiments`** — a runnable project, not a test project. Three
  commands: `walk`, the ζ(2) exhibit; `target`, the π³/ζ(3) run; and
  `survivors`, which reports § 2 step 6's survivor set and draws it as an SVG
  on stdout. Everything in it is `internal`, which is what `.editorconfig`
  expects — CA1515 is suppressed only under `[**/*Tests.cs]`, so a public type
  here fails the build.

### The survivor set needed no reshaping to compute

`walk` and `target` both report a bound read off the last candidate a sweep
proposed, and both print the trend matrix § 2 now keeps as presentation.
`survivors` reports what § 2 step 6 says decides. Nothing in the pipeline had
to change for it: `SurvivorSearch` takes enclosures of the unknown, and
`RatioRun.Iterations` has been yielding them all along as
`RatioIteration.Enclosure.Ratio`. `SurvivorReport.EnclosuresOf` is that
projection and is one line.

**The bound is derived, never picked.** `Q = floor(ε^(-1/2))` from the final
enclosure, which is the depth a generic sweep reaches by § 2's own cost law.
That derivation presumes the denominator axis, so the command sweeps with
`DenominatorSweep` and takes no searcher. Note that `Q`'s axis is
`SurvivorSearch`'s own — it takes a largest denominator — and not the run
searcher's; the two agree here by construction rather than by luck, which is
the distinction § 1 was amended to keep visible.

**Adjacent repeats are dropped before intersecting.** A schedule can ask for a
target the realised bound has already passed, and intersecting an enclosure
with itself refutes nothing while costing a full enumeration. So the count is
quoted per *distinct* enclosure and the chart says so — which basis a count is
quoted on is part of the count, as the ζ(2) walk's three readings already
record.

**Every survivor is printed with its null**, per `../SPEC-rational-ratio.md`
§ 1, which owns the rule as of its ratification on 2026-09-08 and states what
the figure means, why the count is the wrong thing to lead with, and that it is
an *upper* bound. None of that is restated here or in the code; the incident and
the measurement behind it — 40 generic targets at each of three precisions —
are in `../CASEBOOK.md`, which is where a measurement belongs.

What this repository owns is the computation and its two local decisions.
`SurvivorReport.ExpectedSurvivors` takes a **half-width** rather than a whole
interval, so the factor of two in the interval's length is folded into the six;
and π comes from `MachinPi` squared rather than `6/π²` written in as a decimal,
so the figure carries a proven bound like every other value here.

**The cancellation is asserted, not described.**
`ExpectedSurvivors_IsTheSameAtEveryPrecisionOnceTheBoundIsDerived` holds three
precisions three, five and fifteen decades apart to the *same exact rational* —
an identity rather than a measurement, since `DerivedBound` takes
`Q = floor(ε^(-1/2))` and the `ε` cancels the `Q²`. Making the estimate linear
in `q` instead of quadratic turns all three red.

**The cost law is not `target`'s.** The expensive step is the collapse chart's
first point, which counts every rational under `Q` inside the *widest*
enclosure — about `h/ε` candidates for a first target `h` and a last `ε`. One
more decade of schedule is ten times the price, where `target`'s is about
twice. So the schedule was a constant until somebody had measured what the
constant cost, and `SurvivorRun.Refuse` checks the estimate against a measured
budget before spending it.

**Both ends of the schedule are arguments, and the first end is the cheap one.**
The span moved to the caller once a scratchpad probe had shown it, rather than
the providers or the search, to be what confined the exhibit — `halheinrich/Math#64`
leg 3. The estimate is `h*Q²`, so a decade off the last end multiplies `Q²` by
ten while a decade off the first end divides `h` by less, and by no fixed factor.
Measured here in Release at order 3, first ends of 2, 3, 4 and 5 realise `h` of
`2⁻⁸`, `2⁻¹⁰`, `2⁻¹⁵` and `2⁻¹⁷`: four, then thirty-two, then four again, about
eight to the decade on average.

**Those powers of two come from `RatioEnclosure.Of`, not from the providers.**
It calls `Coarsen()` on the propagated bound, which returns the least power of
two at or above it — so every realised half-width is a power of two *by
construction*. `RatioEnclosureTests` pins that in
`Of_CoarsensTheRatioErrorUpToTheNextPowerOfTwo`.
What varies is only which point of that grid a schedule lands on, and
that is set by the first provider step to meet the target. The lumpiness is
therefore the interaction of two known things — step sizes against a power-of-two
grid — and is deterministic and explainable rather than something to be observed.
Said explicitly because the shorter reading, that "the providers halve", sends
the next reader to `MachinPi` and `EulerMaclaurinZeta`, where the mechanism is
not.

The frontier that follows, all measured on this bench in Release at order 3:

| schedule          | Q         | opening step | wall      | µs/cand |
| ----------------- | --------- | ------------ | --------- | ------- |
| `1e-2 .. 1e-8`    | 11,585    | ~1.0M        | 3 s       |         |
| `1e-2 .. 1e-9`    | 32,768    | 4,227,072    | 20 s      | 4.6     |
| `1e-2 .. 1e-10`   | 131,072   | 67,239,936   | *refused* |         |
| `1e-3 .. 1e-10`   | 131,072   | 16,908,288   | 78 s      | 4.6     |
| `1e-3 .. 1e-11`   | 741,455   | 537,612,077  | *refused* |         |
| `1e-4 .. 1e-11`   | 741,455   | 17,518,661   | 167 s     | 9.5     |
| `1e-5 .. 1e-11`   | 741,455   | 4,935,756    | 76 s      | 15.4    |
| `1e-11 .. 1e-11`  | 741,455   | 741,455      | 15 s      | 19.7    |
| `1e-12 .. 1e-12`  | 4,194,304 | 4,194,305    | 47 s      | 11.2    |

So `1e-2 .. 1e-9` is the deepest the budget admits from the *default* first end,
and starting later buys the refused decade back. The default is unchanged,
because a deeper one is a separate judgement about what a first-time reader
should wait for.

**`Budget` counts candidates, and a candidate is not a fixed price.** The last
column above is the measurement: per-candidate cost runs from 4.6 µs at
`Q = 32,768` to about 20 µs at `Q = 741,455`, because the per-candidate work is
a gcd and an exact containment test whose operands grow with the denominator. So
sixty million candidates is a few seconds' worth at the shallow end and a long
wait at the deep end, and the budget bounds the count rather than the wait. The
single-column rows are what establish this: they drive the opening step down to
`Q` itself, so what they time is very nearly the per-candidate cost alone — and
they also show the run's own refinement and sweeps to be seconds, not the hidden
cost they might have been.

`1e-6 .. 1e-12` is the concrete case: the guard admits it, and it had not
finished after twenty minutes here, when it was stopped. That is a lower bound
rather than a measurement of the run. Whether the budget should be a time rather
than a count, or should scale with `Q`, is `halheinrich/Math#64` leg 3's to rule
on; it is not weakened here, and this change is what made it reachable by
argument at all.

**`SurvivorRun.RefuseSchedule` guards shape and nothing else**, and has no
counterpart to `target`'s `MaxLastExponent`: what a schedule costs is priced off
the enclosures a run *realises*, which the argument cannot see, so depth is
refused for its cost by `Refuse` and never for being depth. It does refuse an
exponent below 1 — `Q = floor(ε^(-1/2))` and the null `6εq²/π²` both read the
exponent as a decimal place, and a negative one does not even print — where
`TargetSchedule.Decades` accepts any sign and says so. That is the report's
limit rather than the schedule's, which is why the floor is at the command edge.
Unlike `target`, a single-column schedule is allowed: that command's whole
output is a trend across columns, and this one's is a survivor set that one
enclosure already produces.

### Presentation lives here, not in the library

Ruled in step 6c, the first consumer to need it. Both consumers are inside
`Zeta.Experiments` — the walk's rival panel and the π³/ζ(3) run — so there is
no cross-assembly consumer to serve; `Zeta`'s identity is the composition of
§ 2 and nothing else; and `RealConstants.Experiments` puts its own presentation
in its runner for the same reason. If a consumer ever appears outside the
runner, moving it is additive. `Zeta.Experiments/MatrixReport.cs` carries the
argument in full.

**The runner's pure functions have tests**, because each of them reads
plausibly when wrong: normalising the blame split, truncating an exact rational
to decimal, the survivor report's intersection and derived bound, and the axis
that turns an exact value into a pixel. `Zeta.Tests` is given sight of the
runner's internals for those. Testing a formatter does not make the runner a
test project — `../AGENTS.md` § Exactness discipline separates the two by
whether a run has a known answer and whether it depends on wall-clock time, and
none of these have either property.

**The exactness boundary is the coordinate transform and nothing above it.**
§ 2 permits decimals at presentation and nowhere else, and a log axis needs
one. Every plotted quantity is computed in exact rationals and reaches a chart
as an exact value; `Presentation.DecimalExponent` turns it into a decimal
exponent and `Axis` turns that into a coordinate. Nothing downstream feeds back
into a value, a bound or a decision — in particular the exclusion marks on the
distance chart come from `Approximation.Contains` on exact values, never from
comparing the two doubles the chart drew.

**The ζ(2) walk is here although its answer is known**, and that is not a
violation of the controls-are-tests rule. It is the sniff test for the
presentation, which otherwise renders only π³/ζ(3) and so has no output a
reader can check. It prints a table and depends on wall-clock time, which a
test may not.

## Architecture

Four types, one composition. Each layer of the method in
`../SPEC-rational-ratio.md` § 2 is one of them.

`RatioEnclosure` is step 2 — enclose the power, divide propagating the bound.
`RatioRefiner` is step 4, the halting rule. `RatioRun` is steps 3, 5 and 6 —
sweep, iterate, assemble. `RatioIteration` is one column with the bookkeeping
that `TrendIteration` deliberately does not carry.

`TargetSchedule` is beside them rather than among them: it builds the schedule
a run is driven to and takes no part in the method. `Decade` is the single
error target `Decades` walks across a span, exposed because a test naming one
threshold wants exactly that and not a schedule — three test classes each
carried a private copy of it before 6c, and a fourth spelling inside this type
would have been the same defect in a new place. It is arguably `BigRational`'s
to offer; that library is published and in another repository, which
`../AGENTS.md` § Submodule boundary puts out of reach from here.

### The schedule builder validates its arguments, never the schedule

`TargetSchedule.Decades` emits a strictly decreasing run of powers of ten with
a positive last element **by construction**, so there is nothing in the result
for it to check — and checking anyway would put the rule `RatioRun` owns in a
second place. That is a decision, not an omission. § 6e ruled the same question
one layer up and left `ConstantRun`'s copy of the rule standing rather than
publish a validator: exposing one publishes a policy while leaving every caller
free to skip it, and the real single-sourcing is a validated schedule type
reached through a factory — a two-repo change nobody has planned.

**It is also why there is no overload taking an arbitrary list of exponents.**
Such an overload cannot be valid by construction, because the caller chooses
the order, so it would have to reject a list that does not descend — the
ordering rule written down a second time. A caller wanting exponents no fixed
step reaches, such as `PositiveControlTests`' doubling `4, 8, 16, 32, 64`,
writes them out and hands them to `RatioRun.Execute`, which validates them once
and in the place that owns the rule.

### The halting rule is the substance

§ 2 step 4 halts on the **propagated** ratio error, never on either
component's. The two providers therefore need different depths, and which
depths is not something either of them can answer alone. Deciding it needs the
propagated error split by which operand produced it, and that split is what
`RatioEnclosure` adds.

The propagated bound is `(|b|α + |a|β) / ((|b| − β)|b|)`: one numerator term
per operand over a denominator both share. Their proportion is therefore the
operands' proportion of the whole, whatever the denominator is.
`RatioEnclosure` divides `PropagatedError` in that proportion, which comes out
exactly `α/(|b| − β)` for the power and `|a|β/((|b| − β)|b|)` for the divisor.

**Derived from the total, never recomputed from the formula.** The propagation
rule lives in `Approximation.Divide` and must live in one place; what is
written here is the weaker, separate fact that the numerator has one term per
operand.

`RatioRefiner` then advances whichever provider owns the larger share, one
step at a time, until the ratio's error meets the target. It terminates: if it
did not, some provider would be advanced infinitely often, its bound would
tend to zero, its share with it, and the other provider would be advanced
instead — so both are, both bounds vanish, and the propagated error vanishes
with them.

**Inverting that comparison does not fail, it hangs.** Advancing the provider
whose error is already negligible drives its bound to zero while the other's
stays put, so the propagated error plateaus above every target. That is the
termination argument seen from the other side, and it is why a mutation of
this line times CI out rather than turning a test red.

### Coarsening, and which error means what

`RatioEnclosure` carries two figures for the same quantity, because they do
different jobs. `PropagatedError` is the exact bound `Approximation.Divide`
derived, and it is what the two shares explain. `Ratio.MaxError` is that
rounded up to the next power of two by `Coarsen()`, and it is what a search is
run against and what the halting rule reads.

§ 5 rules the coarsening in: widening a bound is always sound, it discards
only digits nobody reads, and it stops bounds accumulating height as fast as
the values do. Halting on the coarsened figure rather than the exact one means
a target that is met is met **in the enclosure that gets used**, at the price
of targets effectively snapping to powers of two.

### Why there is no stopping rule

`RatioRun` is driven to a fixed sequence of targets and the whole matrix is
read afterwards. A candidate holding steady across iterations is not evidence:
measured runs have shown one hold for two consecutive iterations and then move
on, twice, so any "unchanged for *k* rounds" rule with *k* = 2 gives a false
positive on cases that have actually been observed. There is deliberately no
property here reporting stability, convergence or an answer, for the same
reason `TrendMatrix` has none.

The schedule of targets is the caller's. How far a run should go, and in how
many columns, is a property of the question being asked; § 2 fixes only that
the run is driven to a *fixed* target rather than stopped on what the output
looks like.

### Which candidates reach the matrix

Every candidate every iteration's search yielded, not only the terminating
one. A matrix row is dense, so a candidate first surfaced late still carries a
distance for every earlier column — and the early low-height candidates are
exactly the rows a plateau is read from.

`TrendIteration` leaves that choice to the caller on purpose, so a consumer
wanting to watch a rational no search produced — a positive control such as 6,
90 or 945 — builds its own matrix from `RatioRun.Iterations`, adding that
candidate to any one iteration's contribution.

## Public API

Namespace `HalHeinrich.Numerics`.

```csharp
public sealed class RatioEnclosure
{
    public Approximation PowerBase { get; }      // the base, as handed in
    public Approximation Power { get; }          // base^exponent, via Pow
    public Approximation Divisor { get; }
    public Approximation Ratio { get; }          // Power / Divisor, error coarsened
    public BigRational PropagatedError { get; }  // before coarsening
    public BigRational PowerShare { get; }       // of PropagatedError
    public BigRational DivisorShare { get; }     // of PropagatedError

    public static RatioEnclosure Of(Approximation powerBase, int exponent,
                                    Approximation divisor);
}

public sealed class RatioRefiner : IDisposable
{
    public RatioRefiner(IRealConstant powerBase, int exponent, IRealConstant divisor);

    public int Exponent { get; }
    public int BaseStep { get; }
    public int DivisorStep { get; }
    public RatioEnclosure Current { get; }

    public void RefineTo(BigRational targetError);
    public void Dispose();
}

public sealed class RatioIteration
{
    public BigRational TargetError { get; }
    public int BaseStep { get; }
    public int DivisorStep { get; }
    public RatioEnclosure Enclosure { get; }
    public TrendIteration Trend { get; }
    public IReadOnlyList<RationalCandidate> Candidates { get; }
    public RationalCandidate Simplest { get; }
}

public sealed class RatioRun
{
    public static RatioRun Execute(IRealConstant powerBase, int exponent,
                                   IRealConstant divisor,
                                   IEnumerable<BigRational> targetErrors,
                                   IRationalApproximator? approximator = null);

    public int Exponent { get; }
    public IReadOnlyList<RatioIteration> Iterations { get; }
    public TrendMatrix Matrix { get; }
}

public static class TargetSchedule
{
    public static BigRational Decade(int exponent);   // 10^-exponent

    // 10^-first ... 10^-last, taking `step` decades at a time. Valid by
    // construction; validates its own arguments and not the schedule rule.
    public static IReadOnlyList<BigRational> Decades(int firstExponent,
                                                    int lastExponent,
                                                    int step = 1);
}
```

Contracts a caller is held to:

- `exponent` is at least 1. Zero drops the base from the result and a negative
  one inverts it, and neither is a shape this pipeline is asked for.
- `targetErrors` is strictly decreasing with a positive last element. A repeat
  is a duplicate column rather than fresh evidence, and a larger target cannot
  be honoured because a bound already proven tighter is not un-proven. Empty
  is allowed and gives an honestly empty run.
- `RatioRefiner` holds a live enumerator over each provider's `Refinements()`,
  so it is disposable and refinement is incremental across every call:
  reaching step *n* costs *n* + 1 pulls in total, not that many per target.

Every one of these types is a **sealed class**, not a record struct, because
none of them has a coherent default. An all-default `RatioEnclosure` would
claim a quotient by exactly zero. `Approximation` and `RationalCandidate` are
structs because their defaults *are* meaningful states; these are not that
case, and they follow `TrendIteration` / `TrendRow` / `TrendMatrix` instead.

## Pitfalls

- **`Pow(n)` is not repeated multiplication, and π^n must go through it.**
  `a * a` treats its operands as independent unknowns, so `0 ± 1` squared
  yields `[−1, 1]` — containing negatives no square can take — while `Pow(2)`
  re-centres from the endpoints and yields `[0, 1]`. That is interval
  arithmetic's dependency problem, and a future edit reaching for `*` to build
  a power reintroduces it rather than saving a call.

- **`Power` does not carry the base, and no root recovers it.** `Pow(n)`
  re-centres on the exact image of the input interval, so it discards which
  interval produced that image — `Power.Value` is generally not the base's
  value raised to *n*. `PowerBase` is retained for that reason: a consumer
  wanting π beside π^n otherwise has to re-run the provider to the step the
  refiner reached, reconstructing from the outside the operand the composition
  already held. It is the base's own bound and not a share of anything; it
  answers a different question from `PowerShare`, and the two differ by every
  factor `Pow` and `Divide` introduce between them.

- **Nothing here multiplies two enclosures at all**, which is why
  multiplication's load-bearing second-order term never enters this member.
  The first-order form of that bound is *unsound* rather than merely loose, and
  narrow enclosures never expose it. It is `Approximation.Multiply`'s to
  carry; the moment anything here multiplies, it becomes this member's problem
  too.

- **The kind of bound a run proves follows its searcher, and the two must never
  be described together.** `README.md` § What a result from this bench means
  states the rule; what matters here is that both searchers are in use in one
  runner — `walk` drives `HeightSweep`, `target` drives `DenominatorSweep` — so
  a reporting site cannot assume either. Ask
  `HeightSweep.SearchesNumerators(enclosure)`, which exists precisely so the
  answer is not re-derived from `|Value| > 1` at every site. Caught in step 6c
  by reading the walk's own output: it claimed a denominator bound of 1, and
  `6/1` is inside the enclosure.

- **A divisor whose enclosure contains zero is refined, never caught.**
  `Approximation.Divide` throws on one even when its `Value` is non-zero,
  because such a divisor has not been computed accurately enough to divide by.
  Catching that and continuing would treat a statement about accuracy as an
  error to work around. Every provider `RealConstants` currently ships already
  excludes zero at step 0, so this path has no real-provider exercise and
  exists in the tests only under a stub.

- **This repository does not build standalone**, and the chain is three hops.
  See § Depends on and `README.md` § Building.

- **The reference sweep is deliberately slow and unbounded**, so a target
  chosen without regard to what it implies is the way to make a run take
  forever. Ruling out every rational of **height** below *H* needs error below
  *H*⁻² — `../SPEC-rational-ratio.md` § 2's law, and it is stated on height
  rather than on denominator, which this bullet had wrong until step 6c. The
  searcher's depth tracks ε^(−1/2) for a generic target; a target pinned just
  outside a low-height rational *p*/*q*₀ costs about 1/(2*q*₀ε) instead, and
  at ε = 1e−18 the two differ by some 2.4×10⁸. Which regime a real target is
  in cannot be known in advance, because it is the question being asked, so a
  budget is a **measured depth plus a hard cap** and never a computed bound.
  `Zeta.Experiments` carries one and `Zeta.Tests/TargetRunGuardTests` holds it
  to the measurement.

- **A control belongs in `Zeta.Tests`; a target with no known answer does
  not.** `../AGENTS.md` § Exactness discipline draws that line, and
  `Zeta.Experiments` is where the second kind lives. A long run that prints a
  table has no pass or fail and must not masquerade as a test.

## Subproject-internal next steps

- ~~The library composes; it does not yet report.~~ Settled in step 6c:
  `TrendMatrix` presentation lives beside the runner, in
  `Zeta.Experiments/MatrixReport.cs`. See § Layout for the argument. The
  library still reports nothing, and should not start.
- ~~No target-schedule helper.~~ `TargetSchedule.Decades` closed this in step
  6c, at the second consumer. What is *not* closed is one layer up: the same
  helper would serve `RationalApproximation`'s `ConstantRun`, and the ordering
  rule it declines to restate still exists twice — in `RatioRun.FaultInTargets`
  and in `ConstantRun.FaultInTargets`. § 6e ruled the fix is a validated
  schedule type through a factory, an unplanned two-repo change, and left the
  duplication standing. Nothing here may cross that boundary to fix it.
