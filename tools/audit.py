"""Compare the wiki snapshot with the installed beta and shipped coaching tables.
Usage: python3 tools/audit.py /path/to/game.pck [--write]
Default: check committed inventory and exit nonzero on drift."""
import csv
import io
import re
import sys
from collections import Counter
from pathlib import Path
from catalog import english_cards

ROOT = Path(__file__).resolve().parents[1]
def table(name):
    rows=list(csv.reader((ROOT/"mod"/name).read_text().splitlines(),delimiter="\t"))
    entries=dict((row[0],row[1:]) for row in rows)
    if len(entries)!=len(rows): raise ValueError(f"Duplicate ID in {name}")
    return entries
def norm(value):
    return re.sub("[^a-z0-9]", "", value.lower())

def audit(pack):
    roles, relics, bosses = [table(n+".tsv") for n in ("roles","relics","bosses")]
    potions=table("potions.tsv")
    fit=table("relic-fit.tsv")
    wiki=list(csv.DictReader((ROOT/"audit/wiki.tsv").read_text().splitlines(),delimiter="\t"))
    pages=dict(cards="Cards_List",potions="Potions_List",events="Events_List",relics="Relics_List")
    matched=set()
    rows=[]
    for kind,suffix in {"cards":"title","relics":"title","potions":"title","characters":"title","encounters":"title",
                        "monsters":"name","events":"title","ancients":"epithet","powers":"title","enchantments":"title",
                        "afflictions":"title","orbs":"title","acts":"title"}.items():
        data=english_cards(pack,kind)
        titles={k.split('.')[0]:v for k,v in data.items() if k.count('.')==1 and k.endswith('.'+suffix)}
        for k,v in data.items():
            if k.split('.')[1:2]==[suffix]: titles.setdefault(k.split('.')[0],v)
        if kind=="cards" and titles.keys()!=roles.keys(): raise ValueError("Shipped card IDs differ from beta titles")
        if kind=="encounters" and {k for k in titles if k.endswith('_BOSS')}!=bosses.keys(): raise ValueError("Boss profile drift")
        if kind=="relics" and relics.keys()-titles.keys(): raise ValueError("Unknown relic trigger IDs")
        if kind=="relics" and fit.keys()!=titles.keys(): raise ValueError("Relic index drift")
        for id,title in sorted(titles.items()):
            names={norm(id),norm(title)}
            if kind=="cards" and id.startswith(("STRIKE_","DEFEND_")): names={norm(id)}
            matches=[w for w in wiki if w['page']==pages.get(kind) and norm(w['name']) in names
                     and (w['name']==title or w['name'] not in titles.values())]
            for w in matches: matched.add((w['page'],w['name']))
            if kind=="cards": detail="roles: "+", ".join(filter(None,roles[id])) if any(roles[id]) else "ID only; no inferred role"
            elif kind=="relics": detail="trigger: "+relics[id][0] if id in relics else "fit: "+fit[id][0] if fit[id][0] else "ID only; no fit profile"
            elif kind=="encounters": detail="boss tactic" if id in bosses else "live intent path"
            elif kind=="characters": detail="solo coach" if id in {"IRONCLAD","SILENT","REGENT","NECROBINDER","DEFECT","FOOL"} else "nonstandard / placeholder"
            elif kind=="potions": detail="policy: "+potions[id][0] if id in potions else "no timing rule"
            elif kind=="monsters": detail="live intent path; no individual fight verification"
            else: detail="inventory; no per-entry behavior certification"
            rows.append((kind,id,title,"; ".join(w["name"] for w in matches),detail))
            if kind=="potions" and matches and id not in potions: raise ValueError(f"No policy for {id}")
    for w in wiki:
        if (w['page'],w['name']) not in matched: rows.append((w['page'],"", "", w['name'],"wiki only / unresolved name"))
    buf=io.StringIO()
    writer=csv.writer(buf,delimiter='\t',lineterminator='\n')
    writer.writerow(("category","beta_id","beta_label","wiki_match","evidence"));writer.writerows(rows)
    return buf.getvalue(),rows

if __name__=="__main__":
    out,rows=audit(Path(sys.argv[1]))
    path=ROOT/"audit/inventory.tsv"
    if "--write" in sys.argv: path.write_text(out)
    elif not path.exists() or path.read_text()!=out: raise SystemExit("Inventory drift: rerun with --write and review changes")
    print(dict(Counter(r[0] for r in rows)))
    print("Missing wiki matches:",[r[3] for r in rows if not r[1]])
