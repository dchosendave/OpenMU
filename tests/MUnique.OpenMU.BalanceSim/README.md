# PvP balance simulator

Builds every master class from the **live game configuration** (the database the server uses), at the
same stats and gear tier, and lets them hit each other through the server's own damage code
(`AttackableExtensions.CalculateDamageAsync`). The report has time-to-kill and casts-to-kill tables for
every class against every class, plus each class's HP, SD, defense, hit rates, speeds and buffs.

Run it after any balance change (admin panel or SQL) - it reads the database, so no rebuild is needed:

```
cd OpenMU/tests/MUnique.OpenMU.BalanceSim
dotnet run -c Release -p:ci=true -- --out ../../../balance-report.md
```

Options: `--port 5433` (database port), `--stat 32767` (every base stat), `--level 400`,
`--item-level 15`, `--samples 4000` (hits sampled per matchup; more = steadier numbers),
`--duels 10`, `--duel-seconds 180`, `--no-potions`, `--potion-hp 30`, `--potion-sd 25`, `--potion-cooldown 1` (try values without changing the database), `--no-gear`, `--no-buffs`.

The model and what it leaves out are listed at the end of every report.

## Offense and defense per class

`fit.py <report.md>` reads the "Casts to kill" table of a report and separates how strong each class
attacks from how much it can take (a two-way fit), and lists how close each matchup is once both are
evened out - useful to see whether an imbalance is a class's damage, its survivability, or one
specific matchup.
