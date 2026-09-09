# Zeta

The investigation: is **ζ(n) = a·π^n / b** for odd n — equivalently, is
**π^n / ζ(n)** rational?

This repository is the pipeline that asks. It composes rather than provides:
every contract it wires belongs to
[RationalApproximation](https://github.com/halheinrich/RationalApproximation),
every constant to
[RealConstants](https://github.com/halheinrich/RealConstants). It defines no
enclosure, no search and no constant of its own, and it should not start.

Everything is exact rational arithmetic (`BigRational`). There is no `double`
anywhere in a computational path; floating point is excluded for being
*untrackably* inaccurate, not merely inaccurate.

Design: `SPEC-rational-ratio.md` in the
[umbrella repository](https://github.com/halheinrich/Math).

## What a result from this bench means

**Numerics refute and bound; they do not establish.** Nothing finite can show
that a real number *is* rational. What this pipeline produces is a
**denominator bound**: a proof that every rational of denominator at or below
the last one searched misses the enclosure, *for any numerator*. What survives
that bound is read by counting rather than by watching a row: an empty survivor
set is the refutation, and a short one poses a conjecture and nothing stronger.

That limitation is not a caveat added at the end. It is why the pipeline has
no `IsConverged`, no `Answer`, and no stopping rule — see below.

**The kind of bound follows the searcher, and the two must never be described
together.** `DenominatorSweep` enumerates denominators and so proves a
denominator bound. `HeightSweep` above 1 enumerates numerators and so proves a
height bound. Reporting one while running the other is not a wording slip: on
a value near 6 the denominator reached is 1, and "every rational of denominator
at or below 1 is refuted" is flatly false.

## The first run at π³/ζ(3)

Measured on this bench, 2026-09-07: `MachinPi` and `BorweinZetaThree`, twelve
columns from 1e-2 to 1e-13, searched with `DenominatorSweep`. Wall clock ran
32–46 s across three runs; every figure below was identical in all of them, the
arithmetic being exact.

**Every rational of denominator at or below 3,939,832 misses the final
enclosure, for any numerator.** That is the result. It is a refutation, and it
is the whole of what was established.

The simplest rational the evidence still permits is `101625432/3939833`, of
height about 1.0×10⁸. It is not evidence of anything: one column deeper refutes
it and offers another, which is what every column of the run did to its
predecessor.

Two things in the run are worth a reader's attention.

- **`2515594/97525` was the simplest candidate at two consecutive columns, and
  then moved on.** `SPEC-rational-ratio.md` § 2 names that exact value as the
  reason there is no "unchanged for *k* rounds" stopping rule. It appeared
  unprompted in the first real run.
- **`26/1` sits at `|26 − 25.794…|` in every column and never moves**, which is
  what a refuted candidate looks like: the row settles at its true distance
  rather than falling.

**A falling row poses a conjecture and is not even a reliable sign of one.** A
near-miss — a target sitting just outside a simple rational — produces a row
that falls exactly as a genuine find would, flooring only below any precision
reachable here. Nothing in the matrix distinguishes the two cases. Read the
denominator bound, not the row.

Reproduce it with `dotnet run --project Zeta.Experiments -- target`. It runs no
deeper than a ceiling that is a measured number rather than a computed bound;
sweep depth is set by the continued-fraction structure of the value being asked
about, so a computed ceiling would have to trust the extrapolation it exists to
guard against.

## The method

1. **Enclose** π^n and ζ(n) — each a value with a proven bound on its own
   distance from the truth, supplied by `RealConstants`.
2. **Divide, propagating the bound**:
   `|x/y − a/b| ≤ (|b|α + |a|β) / ((|b| − β)|b|)`, valid when `|b| > β`. The
   power goes through `Pow`, which takes the exact image of the interval;
   building it by repeated multiplication would treat the same unknown as two
   independent ones and admit values it cannot take.
3. **Sweep denominators** on the exact ratio: for each *b*, the best numerator
   is `round(x·b)`, so if that misses the enclosure then every rational of
   that denominator misses it.
4. **Halt on the propagated ratio error** — not on either component's. This is
   the part that decides how deep each provider goes, and the two answers are
   very different. For π³/ζ(3) the propagation is about `0.83α + 21.5β`, so
   the denominator's provider owns some three orders of magnitude more of the
   error than the numerator's does. A target read off the numerator alone
   drives the search far past what the evidence supports.
5. **Iterate** — improve both providers, recompute, sweep again.
6. **Take the survivor set.** Fix a denominator bound *Q*; the answer is every
   rational of denominator at or below *Q* that **no** enclosure excludes. A
   candidate outside any one enclosure is not the ratio — permanently, since
   that enclosure contains it — so the set only shrinks, and it is read by
   counting rather than by reading a trend. The trend matrix is still built and
   is still how a reader sees what the search is doing; it no longer decides.

## Why there is no stopping rule

Every truncated series is a rational, so the ratio of two of them is a
rational, so **a sweep run to zero error terminates at exactly zero on every
target, every time** — "discovering" its own truncation point, and a different
one if you run deeper. Carrying the bound is what makes the sweep stop at a
sensible height instead; the intersection across enclosures is what makes the
answer correct. Two jobs, both needed.

Recurrence is not one of them. Measured runs have shown a candidate holding
steady for **two consecutive iterations** and then moving on — twice, once in
a positive control and once on a real target — so any "unchanged for *k*
rounds" rule with *k* = 2 gives a false positive on cases that have actually
been observed. A run here is driven to a fixed sequence of error targets and
the matrix is read afterwards, never stopped early on what the output looks
like.

## Projects

- `Zeta` — the library: the composition, and nothing else.
- `Zeta.Tests` — xUnit, against fixed inputs.
- `Zeta.Experiments` — runnable, not a test project: a target with no known
  answer has no pass or fail.

```powershell
dotnet run --project Zeta.Experiments -- list
dotnet run --project Zeta.Experiments -- walk
dotnet run --project Zeta.Experiments -- target
dotnet run --project Zeta.Experiments -- survivors > survivors.svg
```

`walk` is **π²/ζ(2), whose answer is 6**, driven one provider step at a time:
π, π² and ζ(2) each at its own step with its own bound, the composed ratio, the
blame split, and a ladder of rival rationals being refuted beside it. It is the
control for what `target` prints — `target`'s answer is unknown, so nothing
about that output can be checked by reading it.

`target` is **π³/ζ(3)**, and reports a denominator bound. It runs no deeper
than its ceiling, which is a measured number rather than a computed bound: ask
for more and it tells you what the run would have cost instead of starting it.

`survivors` is the one that reports a **result** rather than a trend. The other
two print a trend matrix, which § 2 keeps as presentation; this prints what
decides — every rational of denominator at or below `Q` that **no** enclosure
excludes. `Q` is derived as `floor(ε^(-1/2))` from the run's own final bound,
the depth a generic sweep would have reached, because a cap chosen by hand
decides how impressive the collapse looks and nothing checks it. That is a
*sizing law about* `DenominatorSweep` and not a report of one: `survivors` runs
no sweep, having no trend matrix to fill, which is what took `survivors 3 2 14`
from 148 s to a second.

Each survivor is printed **with its null**: `6*ε*q²/π²`, how many rationals of
that denominator a *generic* target of this precision would leave standing by
chance. In this run's output π³/ζ(3)'s two survivors price at 0.41 and 0.49,
and the order-2 control's `6/1` at 4.5e-9. Why that figure rather than the
count is what a survivor set is read against, and that it is an *upper* bound,
is `SPEC-rational-ratio.md` § 1, ratified 2026-09-08; the command reports it
because § 1 asks for it wherever a result is.

It takes the order of ζ, defaulting to 2. An even order has an exact answer
§ 1 lists, so the set is checkable by eye; an odd one is the question. Two
charts go to **stdout as one SVG** — the collapse of the survivor count, and
each followed candidate's distance against the half-width that refutes it —
so redirect it as above and open the file. Labels and progress go to stderr,
the same split every command here uses.

After the order it takes **both ends of the schedule or neither**:
`survivors 3 2 12` runs `1e-2 .. 1e-12`, and the default is `1e-2 .. 1e-8`.
One exponent alone is refused rather than guessed at, since it could name
either end and the two readings differ by a decade of cost apiece. How deep is
worth going is a real question and not a free one: every point of the collapse
chart walks the denominators `1..Q` afresh, counting the rationals its own
prefix admits — about `h·Q² + Q` apiece — and the widest prefix usually
dominates the sum, so a decade off the last end costs ten times as much while a
decade off the first end saves about four. Before it starts, the command times a
short sample against the enclosures it has just realised and predicts what the
walk will cost; ask for more than five minutes of it and it prints the price
instead of paying it. The budget is in **seconds rather than candidates**,
because a candidate costs five times more at order 10 than at order 3. It is not
a timed abort — the search then runs to completion, so a slower machine refuses
more runs and reports the same answer on the ones it admits.

## Building

**This repository does not build standalone.** It references `RealConstants`
by `ProjectReference`, that reference escapes the repo, and the chain
continues for two more hops:

```
..\..\RealConstants\RealConstants\RealConstants.csproj
..\..\RationalApproximation\RationalApproximation\RationalApproximation.csproj
..\..\BigRationalLibrary\BigRationalLibrary\BigRationalLibrary.csproj
```

Those resolve only when all four checkouts sit as siblings, as they do inside
the umbrella:

```
Math/
  BigRationalLibrary/
  RationalApproximation/
  RealConstants/
  Zeta/                      <- here
```

A clone of this repository alone cannot restore. This is the accepted price of
the umbrella's `ProjectReference` ruling, not an oversight; the build-and-test
workflow reconstructs that layout rather than pretending otherwise. Only one
reference is written here — the other two arrive transitively, which was
measured rather than assumed.

```powershell
dotnet build
```

## Test

```powershell
dotnet test
```
