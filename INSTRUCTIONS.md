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

## Architecture

Four types, one composition. Each layer of the method in
`../SPEC-rational-ratio.md` § 2 is one of them.

`RatioEnclosure` is step 2 — enclose the power, divide propagating the bound.
`RatioRefiner` is step 4, the halting rule. `RatioRun` is steps 3, 5 and 6 —
sweep, iterate, assemble. `RatioIteration` is one column with the bookkeeping
that `TrendIteration` deliberately does not carry.

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
  forever. Ruling out every rational of denominator below *H* needs error
  below *H*⁻²; the searcher's depth tracks ε^(−1/2). Neither the series nor
  the searcher is the bottleneck at the depths this bench actually runs, but
  the schedule is the caller's and nothing here will refuse an absurd one.

- **A control belongs in `Zeta.Tests`; a target with no known answer does
  not.** `../AGENTS.md` § Exactness discipline draws that line, and this
  repository will grow a runnable project for the second kind. A long run that
  prints a table has no pass or fail and must not masquerade as a test.

## Subproject-internal next steps

- The library composes; it does not yet report. Presentation of a
  `TrendMatrix` — the table `../SPEC-rational-ratio.md` § 2 step 6 describes,
  formatted for a reader — has no home yet, and the first consumer that needs
  one will decide whether it belongs here or beside the runner.
- No target-schedule helper. Both future consumers will want "run to error τ
  in *k* columns", and if they each write it, that is a library gap wearing a
  disguise rather than two callers being specific.
