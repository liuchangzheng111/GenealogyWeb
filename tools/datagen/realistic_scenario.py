# -*- coding: utf-8 -*-
"""单本族谱内的现实向边界场景数据。

目标：
- 保留离异、再婚、同名、跨姓旁支等测试场景；
- 让后续填充按“父母-子女”层级合理展开，避免单人拥有离谱数量的孩子；
- 保证子女出生年份不会超过当前年份，也不会出现未成年父母生育的情况。
"""
from __future__ import annotations

import csv
import uuid
from collections import deque
from datetime import datetime
from typing import Deque, List, Tuple

from demography import (
    MAIDEN_SURNAMES,
    bulk_divorce_roll,
    bulk_marriage_note,
    mother_birth_for_child,
    wedding_year_hetero,
)
from name_pools import bulk_display_name_for_birth, maybe_death_year

CURRENT_YEAR = datetime.now().year
_NS = uuid.UUID("a0f0a0f0-a0f0-40a0-80a0-0000a0f0a0f0")


def person_id(genealogy_id: str, key: str) -> str:
    return str(uuid.uuid5(_NS, f"{genealogy_id}|p|{key}")).lower()


def write_realistic_scenario(
    w_persons: csv.writer,
    w_pc: csv.writer,
    w_m: csv.writer,
    genealogy_id: str,
    created_at: str,
) -> int:
    """写入一组稳定、可展示的边界场景。"""
    g = genealogy_id
    p_id = lambda k: person_id(g, k)

    rows_p: List[Tuple] = []
    rows_pc: List[Tuple] = []
    rows_m: List[Tuple] = []

    def p(key: str, name: str, gender: str | None, birth: int | None, death: int | None, bio: str) -> str:
        pid = p_id(key)
        rows_p.append(
            [
                pid,
                g,
                name,
                gender if gender is not None else "",
                "" if birth is None else str(birth),
                "" if death is None else str(death),
                bio,
                created_at,
            ]
        )
        return pid

    def pc(parent: str, child: str, rel: str) -> None:
        rows_pc.append([g, parent, child, rel])

    def m(a: str, b: str, wed: int | None, div: int | None, note: str) -> None:
        rows_m.append([g, a, b, "" if wed is None else str(wed), "" if div is None else str(div), note])

    # 核心场景人物
    z_dehou = p("z_dehou", "张德厚", "男", 1920, 2010, "族中长辈；曾与李氏离异后再娶。")
    l_xiulan = p("l_xiulan", "李秀兰", "女", 1922, None, "张德厚前妻；离异后再嫁赵氏。")
    w_guiying = p("w_guiying", "王桂英", "女", 1930, None, "张德厚继室；与长子同父异母子女之母。")
    z_jianguo = p("z_jianguo", "张建国", "男", 1950, None, "长子；经历离异与再婚，子女关系复杂。")
    z_li = p("z_li", "张丽", "女", 1952, None, "长女；有一子与堂支同名。")
    z_jianmin = p("z_jianmin", "张建民", "男", 1955, None, "次子；与建国同父异母。")
    zhao_zg = p("zhao_zg", "赵志刚", "男", 1935, 2018, "李秀兰再婚配偶。")
    zhao_lei = p("zhao_lei", "赵磊", "男", 1970, None, "李秀兰与赵志刚之子。")
    sun_mei = p("sun_mei", "孙梅", "女", 1955, None, "张建国第一任妻子。")
    zhou_min = p("zhou_min", "周敏", "女", 1972, None, "张建国第二任妻子。")
    chen_fang = p("chen_fang", "陈芳", "女", 1975, None, "张建国现任妻子。")
    z_peng = p("z_peng", "张鹏", "男", 1982, None, "建国与孙梅之子。")
    wangwei_j = p("wangwei_j", "王伟", "男", 1990, None, "建国线：与堂支另一王伟同名。")
    li_ming = p("li_ming", "李明", "男", 1955, None, "张丽之夫。")
    wangwei_l = p("wangwei_l", "王伟", "男", 1988, None, "张丽线：与堂支王伟同名同字。")
    z_xiaoyu = p("z_xiaoyu", "张小雨", "女", 1998, None, "建国与周敏之女。")
    p_shikao = p("shikao", "张失考", None, None, None, "旧谱未载生年；用于边界测试。")
    p_nog = p("nog", "佚名", "", 1985, None, "性别未填；用于表单与统计边界。")

    # 亲子关系
    pc(z_dehou, z_jianguo, "father")
    pc(l_xiulan, z_jianguo, "mother")
    pc(z_dehou, z_li, "father")
    pc(l_xiulan, z_li, "mother")
    pc(z_dehou, z_jianmin, "father")
    pc(w_guiying, z_jianmin, "mother")
    pc(zhao_zg, zhao_lei, "father")
    pc(l_xiulan, zhao_lei, "mother")
    pc(z_jianguo, z_peng, "father")
    pc(sun_mei, z_peng, "mother")
    pc(z_jianguo, wangwei_j, "father")
    pc(sun_mei, wangwei_j, "mother")
    pc(z_li, wangwei_l, "mother")
    pc(li_ming, wangwei_l, "father")
    pc(z_jianguo, z_xiaoyu, "father")
    pc(zhou_min, z_xiaoyu, "mother")
    pc(z_dehou, p_shikao, "father")
    pc(l_xiulan, p_shikao, "mother")
    pc(z_li, p_nog, "mother")
    pc(li_ming, p_nog, "father")

    # 婚姻事实
    m(z_dehou, l_xiulan, 1942, 1960, "初婚离异")
    m(z_dehou, w_guiying, 1971, None, "再婚")
    m(l_xiulan, zhao_zg, 1966, None, "李氏再婚")
    m(z_jianguo, sun_mei, 1978, None, "首婚")
    m(z_jianguo, zhou_min, 1996, 2006, "二婚离异")
    m(z_jianguo, chen_fang, 2009, None, "再婚现任")
    m(z_li, li_ming, 1980, None, "婚姻稳定")

    for row in rows_p:
        w_persons.writerow(row)
    for row in rows_pc:
        w_pc.writerow(row)
    for row in rows_m:
        w_m.writerow(row)

    return len(rows_p)


def append_chain_filler(
    w_persons: csv.writer,
    w_pc: csv.writer,
    w_m: csv.writer,
    genealogy_id: str,
    anchor_parent_id: str,
    anchor_birth_year: int,
    count: int,
    surname: str,
    created_at: str,
    name_fn,
) -> None:
    """生成一个受控的多代小家庭树，避免单人拥有过多孩子。"""
    if count <= 0:
        return

    root_spouse_birth = anchor_birth_year + 2
    if root_spouse_birth >= CURRENT_YEAR:
        root_spouse_birth = CURRENT_YEAR - 20
    root_spouse_id = str(uuid.uuid4())
    root_spouse_surname = MAIDEN_SURNAMES[(anchor_birth_year + len(genealogy_id)) % len(MAIDEN_SURNAMES)]
    if root_spouse_surname == surname:
        root_spouse_surname = MAIDEN_SURNAMES[(anchor_birth_year + 1) % len(MAIDEN_SURNAMES)]
    root_spouse_name = bulk_display_name_for_birth(root_spouse_surname, 910_000, root_spouse_birth)
    w_persons.writerow(
        [
            root_spouse_id,
            genealogy_id,
            root_spouse_name,
            "女",
            str(root_spouse_birth),
            maybe_death_year(root_spouse_birth, anchor_birth_year),
            "批量填充·配偶",
            created_at,
        ]
    )
    root_wed = wedding_year_hetero(anchor_birth_year, root_spouse_birth, None, anchor_birth_year)
    w_m.writerow([genealogy_id, anchor_parent_id, root_spouse_id, str(root_wed), "", bulk_marriage_note()])

    def create_spouse(child_birth: int, s: int) -> tuple[str, int]:
        spouse_birth = child_birth + ((s % 7) - 3)
        if spouse_birth >= CURRENT_YEAR:
            spouse_birth = CURRENT_YEAR - 1
        if spouse_birth <= child_birth - 8:
            spouse_birth = child_birth - 8
        if spouse_birth >= child_birth + 8:
            spouse_birth = child_birth + 4
        spouse_surname = MAIDEN_SURNAMES[(s + 3) % len(MAIDEN_SURNAMES)]
        if spouse_surname == surname:
            spouse_surname = MAIDEN_SURNAMES[(s + 5) % len(MAIDEN_SURNAMES)]
        spouse_id = str(uuid.uuid4())
        spouse_name = bulk_display_name_for_birth(spouse_surname, 920_000 + s, spouse_birth)
        w_persons.writerow(
            [
                spouse_id,
                genealogy_id,
                spouse_name,
                "女" if s % 2 == 0 else "男",
                str(spouse_birth),
                maybe_death_year(spouse_birth, 100_000 + s),
                "批量填充·配偶",
                created_at,
            ]
        )
        return spouse_id, spouse_birth

    created = 1
    seed = 0
    queue: Deque[tuple[str, int, str, int, int]] = deque()
    active_pairs: list[tuple[str, int, str, int]] = [(anchor_parent_id, anchor_birth_year, root_spouse_id, root_spouse_birth)]

    seed_count = min(160, max(24, count // 40))
    for _ in range(seed_count):
        if created >= count:
            break
        child_birth = anchor_birth_year + 22 + ((seed + len(active_pairs)) % 4)
        if child_birth >= CURRENT_YEAR:
            child_birth = CURRENT_YEAR - 1
        if child_birth <= anchor_birth_year:
            child_birth = anchor_birth_year + 22
        child_id = str(uuid.uuid4())
        child_gender = "男" if created % 2 == 0 else "女"
        w_persons.writerow(
            [
                child_id,
                genealogy_id,
                name_fn(created, child_birth),
                child_gender,
                str(child_birth),
                maybe_death_year(child_birth, created),
                "批量填充-根层",
                created_at,
            ]
        )
        w_pc.writerow([genealogy_id, anchor_parent_id, child_id, "father"])
        w_pc.writerow([genealogy_id, root_spouse_id, child_id, "mother"])
        created += 1

        spouse_id, spouse_birth = create_spouse(child_birth, seed)
        wed = wedding_year_hetero(child_birth, spouse_birth, None, seed)
        w_m.writerow([genealogy_id, child_id, spouse_id, str(wed), "", bulk_marriage_note() + "·树系"])
        queue.append((child_id, child_birth, spouse_id, spouse_birth, 0))
        active_pairs.append((child_id, child_birth, spouse_id, spouse_birth))
        created += 1
        seed += 1

    while queue and created < count:
        father_id, father_birth, mother_id, mother_birth, generation = queue.popleft()
        if father_birth > CURRENT_YEAR - 22:
            continue

        couple_children = 2 if generation >= 8 else 3 if generation >= 4 else 4
        couple_children = min(couple_children, count - created)

        for slot in range(couple_children):
            child_birth = max(father_birth, mother_birth) + 22 + ((seed + slot) % 5)
            if child_birth >= CURRENT_YEAR:
                child_birth = CURRENT_YEAR - 1
            if child_birth <= max(father_birth, mother_birth):
                child_birth = max(father_birth, mother_birth) + 22

            child_id = str(uuid.uuid4())
            child_gender = "男" if (seed + slot) % 2 == 0 else "女"
            w_persons.writerow(
                [
                    child_id,
                    genealogy_id,
                    name_fn(created, child_birth),
                    child_gender,
                    str(child_birth),
                    maybe_death_year(child_birth, created),
                    "批量填充-链式",
                    created_at,
                ]
            )
            w_pc.writerow([genealogy_id, father_id, child_id, "father"])
            w_pc.writerow([genealogy_id, mother_id, child_id, "mother"])
            created += 1

            # 前几代允许更多子女继续繁衍，但仍限制每对父母的子女数上限。
            can_continue = generation < 8 and child_birth <= CURRENT_YEAR - 18 and created < count - 1
            if can_continue:
                spouse_id, spouse_birth = create_spouse(child_birth, seed + slot)
                wed = wedding_year_hetero(child_birth, spouse_birth, None, seed + slot)
                w_m.writerow([genealogy_id, child_id, spouse_id, str(wed), "", bulk_marriage_note() + "·树系"])
                queue.append((child_id, child_birth, spouse_id, spouse_birth, generation + 1))
                active_pairs.append((child_id, child_birth, spouse_id, spouse_birth))
                created += 1

            if created >= count:
                break

        seed += 1

    # 兜底：如果仍有剩余，就补成不再繁衍的叶节点。
    fallback_index = 0
    while created < count:
        father_id, father_birth, mother_id, mother_birth = active_pairs[fallback_index % len(active_pairs)]
        fallback_index += 1
        child_birth = max(father_birth, mother_birth) + 22 + ((created + seed) % 6)
        if child_birth >= CURRENT_YEAR:
            child_birth = CURRENT_YEAR - 1
        if child_birth <= max(father_birth, mother_birth):
            child_birth = max(father_birth, mother_birth) + 22
        child_id = str(uuid.uuid4())
        w_persons.writerow(
            [
                child_id,
                genealogy_id,
                name_fn(created, child_birth),
                "男" if created % 2 == 0 else "女",
                str(child_birth),
                maybe_death_year(child_birth, created),
                "批量填充-叶",
                created_at,
            ]
        )
        w_pc.writerow([genealogy_id, father_id, child_id, "father"])
        w_pc.writerow([genealogy_id, mother_id, child_id, "mother"])
        created += 1
