import csv
from collections import Counter
from pathlib import Path

people = {}
with Path('tools/datagen/out/persons.csv').open(encoding='utf-8') as f:
    for row in csv.DictReader(f):
        people[row['Id']] = row

counts = Counter()
with Path('tools/datagen/out/parent_children.csv').open(encoding='utf-8') as f:
    for row in csv.DictReader(f):
        counts[(row['GenealogyId'], row['ParentId'])] += 1

for (gid, pid), total in counts.most_common(30):
    person = people.get(pid, {})
    print(total, person.get('GivenName', ''), person.get('BirthYear', ''), gid)
