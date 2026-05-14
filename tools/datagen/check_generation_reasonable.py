import csv
from collections import defaultdict
from pathlib import Path

people = {}
with Path('tools/datagen/out/persons.csv').open(encoding='utf-8') as f:
    for row in csv.DictReader(f):
        try:
            birth = int(row['BirthYear']) if row['BirthYear'] else None
        except ValueError:
            birth = None
        people[row['Id']] = {
            'birth': birth,
            'genealogy': row['GenealogyId'],
            'name': row['GivenName'],
        }

violations = []
parent_child_counts = defaultdict(int)
with Path('tools/datagen/out/parent_children.csv').open(encoding='utf-8') as f:
    for row in csv.DictReader(f):
        parent = people.get(row['ParentId'])
        child = people.get(row['ChildId'])
        if parent and child and parent['birth'] is not None and child['birth'] is not None:
            parent_child_counts[row['ParentId']] += 1
            gap = child['birth'] - parent['birth']
            if gap < 18:
                violations.append({
                    'genealogy': row['GenealogyId'],
                    'relationship': row['RelationshipType'],
                    'parent_name': parent['name'],
                    'parent_birth': parent['birth'],
                    'child_name': child['name'],
                    'child_birth': child['birth'],
                    'gap': gap,
                })

print(f'parent_child_rows={sum(parent_child_counts.values())}')
print(f'parents_with_children={len(parent_child_counts)}')
print(f'underage_parent_child_pairs={len(violations)}')
for v in violations[:20]:
    print(v)
