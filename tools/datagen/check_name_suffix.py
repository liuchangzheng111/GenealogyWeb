import csv
import re
from pathlib import Path

pat = re.compile(r"\d+$")
count = 0
bad = 0
with Path('tools/datagen/out/persons.csv').open(encoding='utf-8') as f:
    reader = csv.DictReader(f)
    for row in reader:
        count += 1
        if pat.search(row['GivenName']):
            bad += 1

print(f'rows={count}')
print(f'names_with_trailing_digits={bad}')
