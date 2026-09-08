# Lumina evolution thresholds

First evolution (starter to Dawn, Moonlight or Warm Sun): level 15.
Second evolution (stage 1 to stage 2 of that branch): level 30.

PetEvolutionPolicy is applied to overview eligibility, starter options and execution,
stage lists, next-stage execution, preview and administrative detail responses.
Existing persisted stage thresholds of 5/10 are overridden for these three supported
branches; other affinities/stages retain their configured requirements.
This release does not mutate user levels, history, or already acquired forms.
Previously acquired stages remain unlocked even below the new thresholds.
Starter evolution keeps current EXP and calculates the next threshold at the current
level, using the existing EXP configuration.

Deploy the updated backend to activate the policy. A database migration is not required
for these player-facing endpoints. Raw database readers will still see old configured
values until a separately controlled data update; do not treat those as effective policy.

Validation: `dotnet test Walkamon.EvolutionTests/Walkamon.EvolutionTests.csproj`.
Tests cover legacy 5/10 metadata, first evolution 14/15, second evolution 29/30,
all supported branches, and preservation of earned EXP.
