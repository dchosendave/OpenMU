"""Two-way fit of log(casts to kill) = mu + attacker + defender, from a simulator report.
Prints per-lineage damage and damage-taken multipliers that cancel the attacker/defender effects."""
import math, re, sys
text = open(sys.argv[1], encoding="utf-8").read()
section = text.split("### Casts to kill")[1].split("## Classes")[0]
rows = [l for l in section.splitlines() if l.startswith("| **")]
names = [re.match(r"\| \*\*(.+?)\*\*", r).group(1) for r in rows]
m = [[float(c.strip().replace(",", "")) for c in r.split("|")[2:2 + len(rows)]] for r in rows]
n = len(names)
lg = [[math.log(max(v, 0.05)) for v in row] for row in m]
mu = sum(map(sum, lg)) / n / n
att = [sum(lg[a]) / n - mu for a in range(n)]
dfn = [sum(lg[a][d] for a in range(n)) / n - mu for d in range(n)]
order = ["Grand Master", "Blade Master", "High Elf", "Duel Master", "Lord Emperor", "Dimension Master", "Fist Master"]
# attacker effect > 0: needs more casts -> weaker -> more damage; defender effect > 0: tankier -> takes more.
k = math.exp(mu) / 167.0  # pace: about 167 casts to kill (~10 s) without potions
dmg = {names[i]: math.exp(att[i]) * k for i in range(n)}
taken = {names[i]: math.exp(dfn[i]) for i in range(n)}
resid = max(abs(lg[a][d] - mu - att[a] - dfn[d]) for a in range(n) for d in range(n))
print("median casts to kill (neutral):", round(math.exp(mu), 2), " worst matchup-specific deviation: x%.2f" % math.exp(resid))
print("dmg  ", " ".join(f"{dmg[o]:.6f}" for o in order))
print("taken", " ".join(f"{taken[o]:.4f}" for o in order))
# Closeness after normalization: kill time of a->b over b->a (1 = even; 1.2 = b kills 20% faster).
res = [[lg[a][d] - mu - att[a] - dfn[d] for d in range(n)] for a in range(n)]
pairs = sorted(((math.exp(abs(res[a][b] - res[b][a])), names[a], names[b], res[a][b] < res[b][a]) for a in range(n) for b in range(a + 1, n)), reverse=True)
print("pairs by edge (stronger side kills x faster):")
for ratio, a, b, a_faster in pairs:
    print(f"  {ratio:4.2f}  {(a if a_faster else b)} over {(b if a_faster else a)}")
