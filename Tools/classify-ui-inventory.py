"""Assign every discovered native declaration to an open TODO group.

The mapping is inferred triage, not proof of reachability, UI coverage or a pass.
"""
import csv
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AUDIT = ROOT / 'Docs/UI-Reworked/Audit32'


def main():
    with (AUDIT/'ui-todo.csv').open(newline='',encoding='utf-8-sig') as f:
        todos = list(csv.DictReader(f))
    rules = sorted((t for t in todos if t['native_match']),key=lambda t:int(t['match_order']))
    compiled = [(t,re.compile(t['native_match'],re.I)) for t in rules]
    with (AUDIT/'native-ui-inventory.csv').open(newline='',encoding='utf-8-sig') as f:
        inventory = list(csv.DictReader(f))
    rows = []
    for row in inventory:
        matches = [t for t,pattern in compiled if pattern.search(row['native_type'])]
        if not matches:
            raise ValueError(f"Unassigned native type: {row['native_type']}")
        chosen = matches[0]
        framework = row['is_abstract'] == 'true' or row['is_interface'] == 'true' or row['is_family_root'] == 'true'
        rows.append(dict(native_type=row['native_type'],category=row['category'],is_abstract=row['is_abstract'],
                         todo_id=chosen['id'],todo_title=chosen['title'],priority=chosen['priority'],
                         disposition='framework/helper declaration; inspect consumers' if framework else 'candidate; confirm access path and actual body',
                         evidence_kind='inferred triage from type identity; not visual coverage',
                         other_matching_groups=';'.join(t['id'] for t in matches[1:] if t['id']!='UI-032'),
                         source_mentions=row['mod_source_mentions'],live_observed='not established per type',status='open'))
    assert len({r['native_type'] for r in rows}) == len(rows)
    with (AUDIT/'native-ui-coverage.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.DictWriter(f,fieldnames=list(rows[0]),lineterminator='\n');writer.writeheader();writer.writerows(rows)
    rule_doc={'schema':'ui-backlog-triage-rules-v1','interpretation':'Priority-ordered name grouping only. Matches are inferred, not evidence of redesign or reachability. A generic message/choice class can implement many flows. Source mentions are not coverage.',
              'rules':[dict(todo_id=t['id'],order=int(t['match_order']),pattern=t['native_match']) for t in rules]}
    (AUDIT/'coverage-rules.json').write_text(json.dumps(rule_doc,indent=2)+'\n',encoding='utf-8')
    summary={'schema':'ui-backlog-triage-summary-v1','todo_groups':len(todos),'open_groups':sum(t['status']=='open' for t in todos),
             'inventory_rows':len(rows),'mapped_rows':len(rows),'unmapped_rows':0,
             'groups':dict(sorted(Counter(r['todo_id'] for r in rows).items())),
             'claim':'Every discovered row is assigned a follow-up group; this is not a percentage of restyled UI.',
             'inputs':{name:hashlib.sha256((AUDIT/name).read_bytes()).hexdigest() for name in ['ui-todo.csv','native-ui-inventory.csv']}}
    (AUDIT/'coverage-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(summary,indent=2))


if __name__=='__main__':
    main()
