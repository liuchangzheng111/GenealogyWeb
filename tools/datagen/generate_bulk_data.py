#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成课程要求的批量 CSV：10 本族谱、核心成员合计 100_000 名 Persons（另为双亲/婚姻补全约数百名配偶行）；其中一本 >=50_000 且含 30 代直系父链；
第一本支谱含「再婚/同名/半同胞」等现实向场景 + marriages.csv，其余支谱为常见名池填充。
用法：python tools/datagen/generate_bulk_data.py --owner-id <Guid> --output-dir tools/datagen/out
"""
from __future__ import annotations

import argparse
import csv
import os
import uuid
import sys
from pathlib import Path

_HERE = Path(__file__).resolve().parent
if str(_HERE) not in sys.path:
    sys.path.insert(0, str(_HERE))

from datetime import datetime, timezone
from typing import List, Sequence, Tuple

from demography import (
    MAIDEN_SURNAMES,
    bulk_divorce_roll,
    bulk_marriage_note,
    mother_birth_for_child,
    wedding_year_hetero,
)
from name_pools import bulk_display_name_for_birth, maybe_death_year
from realistic_scenario import append_chain_filler, person_id, write_realistic_scenario


def _parse_guid(s: str) -> str:
    s = s.strip().strip("{}")
    uuid.UUID(s)
    return s.lower()


def _write_genealogies(
    path: str,
    genealogy_ids: Sequence[str],
    owner_id: str,
    created_at: str,
    metas: Sequence[Tuple[str, str]],
) -> None:
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["Id", "Title", "Surname", "CompiledAt", "CreatedByUserId", "CreatedAt"])
        for i, gid in enumerate(genealogy_ids):
            title, surname = metas[i]
            w.writerow([gid, title, surname, "", owner_id, created_at])


def _write_genealogy_users(path: str, rows: Sequence[Tuple[str, str, str, str]]) -> None:
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["GenealogyId", "UserId", "Role", "InvitedByUserId", "InvitedAt"])
        for gid, uid, role, invited_at in rows:
            w.writerow([gid, uid, role, "", invited_at])


def _append_persons_and_edges_big(
    w_persons: csv.writer,
    w_pc: csv.writer,
    w_m: csv.writer,
    gid: str,
    target_count: int,
    spine_len: int,
    created_at: str,
    surname: str,
    name_base: int,
) -> None:
    assert target_count >= spine_len
    ids = [str(uuid.uuid4()) for _ in range(target_count)]
    spine_ids = ids[:spine_len]
    # 父链边均标为 father：父节点须为男，避免「女性却为 father 边」的违和与展示歧义。
    spine_births: list[int] = []
    y = 1180 + (name_base % 9)
    for i in range(spine_len):
        spine_births.append(y)
        if i < spine_len - 1:
            gap = 20 + ((i * 11 + name_base) % 21)  # 两代间隔约 20–40 岁
            y = y + gap

    for i in range(spine_len):
        birth = spine_births[i]
        name = bulk_display_name_for_birth(surname, name_base + i, birth)
        death = maybe_death_year(birth, name_base + i)
        w_persons.writerow(
            [spine_ids[i], gid, name, "男", str(birth), death, f"主干第{i}代", created_at]
        )

    # 主干：每位父亲配母亲（与下一子代同父母），并写婚姻；侧枝与对应子代共母。
    spine_mother_ids: list[str] = []
    for i in range(spine_len - 1):
        f_b, c_b = spine_births[i], spine_births[i + 1]
        mb = mother_birth_for_child(c_b, f_b, name_base + i * 131)
        mid = str(uuid.uuid4())
        spine_mother_ids.append(mid)
        maiden = MAIDEN_SURNAMES[(name_base + i) % len(MAIDEN_SURNAMES)]
        m_name = bulk_display_name_for_birth(maiden, 900_000 + name_base + i, mb)
        w_persons.writerow(
            [
                mid,
                gid,
                m_name,
                "女",
                str(mb),
                maybe_death_year(mb, name_base + 50_000 + i),
                f"配{surname}氏·主干第{i + 1}代母",
                created_at,
            ]
        )
        wed = wedding_year_hetero(f_b, mb, c_b, name_base + 17 * i)
        div = bulk_divorce_roll(name_base + 91 * i, wed, c_b)
        note = bulk_marriage_note() + ("·离异" if div is not None else "")
        w_m.writerow([gid, spine_ids[i], mid, str(wed), "" if div is None else str(div), note])

    for i in range(spine_len - 1):
        w_pc.writerow([gid, spine_ids[i], spine_ids[i + 1], "father"])
        w_pc.writerow([gid, spine_mother_ids[i], spine_ids[i + 1], "mother"])

    for j in range(spine_len, target_count):
        pid = (j * 7919) % (spine_len - 1)
        parent_id = spine_ids[pid]
        parent_birth = spine_births[pid]
        mother_id = spine_mother_ids[pid]
        # 生育年龄约 18–42 岁，满足库触发器「父年 < 子年」
        child_birth = parent_birth + 18 + ((j + name_base) % 25)
        gender = "男" if j % 2 == 0 else "女"
        name = bulk_display_name_for_birth(surname, name_base + j, child_birth)
        death = maybe_death_year(child_birth, name_base + j)
        w_persons.writerow([ids[j], gid, name, gender, str(child_birth), death, "主干侧枝", created_at])
        w_pc.writerow([gid, parent_id, ids[j], "father"])
        w_pc.writerow([gid, mother_id, ids[j], "mother"])


def _append_persons_and_edges_small(
    w_persons: csv.writer,
    w_pc: csv.writer,
    w_m: csv.writer,
    gid: str,
    target_count: int,
    created_at: str,
    surname: str,
    name_base: int,
) -> None:
    assert target_count >= 2
    ids = [str(uuid.uuid4()) for _ in range(target_count)]
    chain = min(80, target_count)
    # 在「最早出生年」与「链末端出生年」之间分配代际间隔，避免 80 代全挤进两三百年或反冲进未来。
    youngest = 1988 + (name_base % 28)
    earliest_floor = 1260
    n_gaps = max(1, chain - 1)
    span_budget = max(youngest - earliest_floor, n_gaps * 22)
    base_gap = max(22, min(40, span_budget // n_gaps))
    gaps: list[int] = []
    for i in range(n_gaps):
        jitter = ((i * 7 + name_base) % 7) - 3
        gaps.append(max(18, min(45, base_gap + jitter)))
    total_gap = sum(gaps)
    if total_gap > youngest - earliest_floor:
        scale = (youngest - earliest_floor) / total_gap
        gaps = [max(18, int(g * scale)) for g in gaps]
        total_gap = sum(gaps)
        while total_gap > youngest - earliest_floor:
            mi = max(range(len(gaps)), key=lambda k: gaps[k])
            if gaps[mi] <= 18:
                break
            gaps[mi] -= 1
            total_gap -= 1

    oldest = youngest - sum(gaps)
    births: list[int] = []
    y = oldest
    for i in range(chain):
        births.append(y)
        if i < chain - 1:
            y += gaps[i]

    for i in range(chain):
        b = births[i]
        w_persons.writerow(
            [
                ids[i],
                gid,
                bulk_display_name_for_birth(surname, name_base + i, b),
                "男",
                str(b),
                maybe_death_year(b, name_base + i),
                f"{surname}氏批量支系·主干",
                created_at,
            ]
        )
    chain_mother_ids: list[str] = []
    for i in range(chain - 1):
        f_b, c_b = births[i], births[i + 1]
        mb = mother_birth_for_child(c_b, f_b, name_base + 4000 + i)
        mid = str(uuid.uuid4())
        chain_mother_ids.append(mid)
        maiden = MAIDEN_SURNAMES[(name_base + i + 3) % len(MAIDEN_SURNAMES)]
        if maiden == surname:
            maiden = MAIDEN_SURNAMES[(name_base + i + 4) % len(MAIDEN_SURNAMES)]
        m_name = bulk_display_name_for_birth(maiden, 800_000 + name_base + i, mb)
        w_persons.writerow(
            [
                mid,
                gid,
                m_name,
                "女",
                str(mb),
                maybe_death_year(mb, name_base + 60_000 + i),
                f"配{surname}氏·支系主干",
                created_at,
            ]
        )
        wed = wedding_year_hetero(f_b, mb, c_b, name_base + 5 * i)
        div = bulk_divorce_roll(name_base + 111 * i, wed, c_b)
        note = bulk_marriage_note() + ("·离异" if div is not None else "")
        w_m.writerow([gid, ids[i], mid, str(wed), "" if div is None else str(div), note])

    for i in range(chain - 1):
        w_pc.writerow([gid, ids[i], ids[i + 1], "father"])
        w_pc.writerow([gid, chain_mother_ids[i], ids[i + 1], "mother"])

    hub_idx = chain // 2
    hub_id = ids[hub_idx]
    hub_birth = births[hub_idx]
    hub_maiden = MAIDEN_SURNAMES[(name_base + hub_idx + 9) % len(MAIDEN_SURNAMES)]
    if hub_maiden == surname:
        hub_maiden = MAIDEN_SURNAMES[(name_base + hub_idx + 10) % len(MAIDEN_SURNAMES)]
    first_leaf_birth = min(2022, hub_birth + 20 + ((name_base) % 22))
    if first_leaf_birth <= hub_birth:
        first_leaf_birth = hub_birth + 22
    hub_wife_birth = mother_birth_for_child(first_leaf_birth, hub_birth, name_base + 777)
    hub_wife_id = str(uuid.uuid4())
    hw_name = bulk_display_name_for_birth(hub_maiden, 810_000 + name_base + hub_idx, hub_wife_birth)
    w_persons.writerow(
        [
            hub_wife_id,
            gid,
            hw_name,
            "女",
            str(hub_wife_birth),
            maybe_death_year(hub_wife_birth, name_base + 88_888),
            f"配{surname}氏·叶节点之母",
            created_at,
        ]
    )
    wed_hub = wedding_year_hetero(hub_birth, hub_wife_birth, first_leaf_birth, name_base + 999)
    div_h = bulk_divorce_roll(name_base + 3333, wed_hub, first_leaf_birth)
    w_m.writerow(
        [
            gid,
            hub_id,
            hub_wife_id,
            str(wed_hub),
            "" if div_h is None else str(div_h),
            bulk_marriage_note() + ("·离异" if div_h is not None else "") + "·叶系",
        ]
    )

    for j in range(chain, target_count):
        child_birth = min(2022, hub_birth + 20 + ((j - chain + name_base) % 22))
        if child_birth <= hub_birth:
            child_birth = hub_birth + 20
        w_persons.writerow(
            [
                ids[j],
                gid,
                bulk_display_name_for_birth(surname, name_base + j, child_birth),
                "女" if j % 2 else "男",
                str(child_birth),
                maybe_death_year(child_birth, name_base + j),
                f"{surname}氏批量叶节点",
                created_at,
            ]
        )
        w_pc.writerow([gid, hub_id, ids[j], "father"])
        w_pc.writerow([gid, hub_wife_id, ids[j], "mother"])


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate bulk genealogy CSVs for MySQL LOAD DATA.")
    parser.add_argument("--owner-id", required=True, help="Existing Users.Id (e.g. demo user Guid).")
    parser.add_argument("--output-dir", default="tools/datagen/out", help="Output directory for CSV files.")
    args = parser.parse_args()

    owner_id = _parse_guid(args.owner_id)
    os.makedirs(args.output_dir, exist_ok=True)

    created_at = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
    genealogy_ids = [str(uuid.uuid4()) for _ in range(10)]

    small_sizes: List[int] = []
    rem = 50_000 % 9
    base = 50_000 // 9
    for i in range(9):
        small_sizes.append(base + (1 if i < rem else 0))

    metas: List[Tuple[str, str]] = [
        ("李氏大宗谱·三十代主干（课程大谱）", "李"),
        ("张氏·再婚同名与跨姓分支（场景+填充）", "张"),
        ("王氏族学支系·批量支谱之一", "王"),
        ("陈氏家族·批量支谱之二", "陈"),
        ("刘氏家族·批量支谱之三", "刘"),
        ("杨氏家族·批量支谱之四", "杨"),
        ("黄氏家族·批量支谱之五", "黄"),
        ("周氏家族·批量支谱之六", "周"),
        ("吴氏家族·批量支谱之七", "吴"),
        ("赵氏家族·批量支谱之八", "赵"),
    ]

    g_path = os.path.join(args.output_dir, "genealogies.csv")
    gu_path = os.path.join(args.output_dir, "genealogy_users.csv")
    p_path = os.path.join(args.output_dir, "persons.csv")
    pc_path = os.path.join(args.output_dir, "parent_children.csv")
    m_path = os.path.join(args.output_dir, "marriages.csv")

    _write_genealogies(g_path, genealogy_ids, owner_id, created_at, metas)

    gu_rows = [(gid, owner_id, "Owner", created_at) for gid in genealogy_ids]
    _write_genealogy_users(gu_path, gu_rows)

    branch_surnames = ["王", "陈", "刘", "杨", "黄", "周", "吴", "赵"]

    with open(p_path, "w", newline="", encoding="utf-8") as f_p, open(
        pc_path, "w", newline="", encoding="utf-8"
    ) as f_pc, open(m_path, "w", newline="", encoding="utf-8") as f_m:
        w_p = csv.writer(f_p)
        w_pc = csv.writer(f_pc)
        w_m = csv.writer(f_m)
        w_p.writerow(["Id", "GenealogyId", "GivenName", "Gender", "BirthYear", "DeathYear", "Bio", "CreatedAt"])
        w_pc.writerow(["GenealogyId", "ParentId", "ChildId", "RelationshipType"])
        w_m.writerow(["GenealogyId", "SpouseAId", "SpouseBId", "MarriedAtYear", "DivorcedAtYear", "Note"])

        big_gid = genealogy_ids[0]
        _append_persons_and_edges_big(w_p, w_pc, w_m, big_gid, 50_000, 31, created_at, "李", 0)

        for idx, size in enumerate(small_sizes):
            gid = genealogy_ids[1 + idx]
            if idx == 0:
                n_scenario = write_realistic_scenario(w_p, w_pc, w_m, gid, created_at)
                filler = size - n_scenario
                if filler < 0:
                    raise SystemExit(
                        f"第一本支谱目标人数 {size} 小于场景人数 {n_scenario}，请调大 small_sizes[0] 分配。"
                    )
                append_chain_filler(
                    w_p,
                    w_pc,
                    w_m,
                    gid,
                    person_id(gid, "z_jianmin"),
                    1955,
                    filler,
                    "张",
                    created_at,
                    lambda i, birth_y: bulk_display_name_for_birth("张", 70_000 + i, birth_y),
                )
            else:
                sur = branch_surnames[idx - 1]
                base = 12_000 * idx
                _append_persons_and_edges_small(w_p, w_pc, w_m, gid, size, created_at, sur, base)

    spine_len = 31
    big_mothers = spine_len - 1

    def _small_extra_persons(s: int) -> int:
        return min(80, s)

    total_persons = 50_000 + big_mothers + small_sizes[0] + sum(
        s + _small_extra_persons(s) for s in small_sizes[1:]
    )
    meta_path = os.path.join(args.output_dir, "manifest.txt")
    with open(meta_path, "w", encoding="utf-8") as mf:
        mf.write(f"owner_user_id={owner_id}\n")
        mf.write("genealogy_count=10\n")
        mf.write(f"persons_total={total_persons}\n")
        mf.write(f"big_genealogy_id={genealogy_ids[0]}\n")
        mf.write("big_genealogy_persons=50000+30_spouse spine_generations=30\n")
        mf.write(f"scenario_genealogy_id={genealogy_ids[1]} marriages_csv=marriages.csv\n")
        for i, s in enumerate(small_sizes):
            mf.write(f"small_genealogy_{i + 1}_id={genealogy_ids[1 + i]} persons={s}\n")

    print(f"Wrote CSV under {args.output_dir}")
    print(f"  persons rows: {total_persons} (含批量配偶/母亲；原 10 万核心成员 + 社会学补全)")
    print(f"  manifest: {meta_path}")


if __name__ == "__main__":
    main()
