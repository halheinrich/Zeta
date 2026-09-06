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
the last one searched misses the enclosure, *for any numerator*. A run whose
candidate keeps improving poses a conjecture and nothing stronger.

That limitation is not a caveat added at the end. It is why the pipeline has
no `IsConverged`, no `Answer`, and no stopping rule — see below.

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
6. **Read the trend matrix.** Rows are candidates, columns iterations, cells
   the exact `|a/b − x_k|`. A row falling towards zero is the candidate the
   evidence favours; every other row settles at that candidate's true distance
   from the constant.

## Why there is no stopping rule

Every truncated series is a rational, so the ratio of two of them is a
rational, so **a sweep run to zero error terminates at exactly zero on every
target, every time** — "discovering" its own truncation point, and a different
one if you run deeper. Carrying the bound is what makes the sweep stop at a
sensible height instead; reading the whole matrix is what makes the answer
correct. Two jobs, both needed.

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
