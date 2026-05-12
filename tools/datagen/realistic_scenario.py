# -*- coding: utf-8 -*-
"""
单本族谱内的「现实向」边界场景数据（人数适中，覆盖测试关注点）：
- 离异 + 再婚 + 多段婚姻（Marriages 含 MarriedAtYear / DivorcedAtYear）
- 同谱同名不同人（两名「王伟」）
- 同父异母 / 半同胞
- 随母再婚产生的旁支（不同姓）
- 出生年缺失、性别未填、较长简介
"""
from __future__ import annotations

import csv
import hashlib
import uuid
from typing import Callable, List, Tuple

from demography import (
    MAIDEN_SURNAMES,
    bulk_divorce_roll,
    bulk_marriage_note,
    mother_birth_for_child,
    wedding_year_hetero,
)
from name_pools import bulk_display_name_for_birth, maybe_death_year

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
    """
    写入本族谱的场景人物、亲子边、婚姻事实。
    返回本函数写入的 Person 行数（不含后续填充）。
    """
    g = genealogy_id
    P = lambda k: person_id(g, k)

    rows_p: List[Tuple] = []
    rows_pc: List[Tuple] = []
    rows_m: List[Tuple] = []

    def p(
        key: str,
        name: str,
        gender: str | None,
        birth: int | None,
        death: int | None,
        bio: str,
    ) -> str:
        pid = P(key)
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
        rows_m.append(
            [
                g,
                a,
                b,
                "" if wed is None else str(wed),
                "" if div is None else str(div),
                note,
            ]
        )

    # --- 人物（出生年严格满足亲子触发器）---
    z_dehou = p("z_dehou", "张德厚", "男", 1920, 2010, "族中长辈；曾与李氏离异后再娶。")
    l_xiulan = p("l_xiulan", "李秀兰", "女", 1922, None, "张德厚前妻；离异后再嫁赵氏。")
    w_guiying = p("w_guiying", "王桂英", "女", 1930, None, "张德厚继室；与长子同父异母子女之母。")
    z_jianguo = p("z_jianguo", "张建国", "男", 1950, None, "长子；经历离异与再婚，子女关系复杂。")
    z_li = p("z_li", "张丽", "女", 1952, None, "长女；有一子与堂支「王伟」同名。")
    z_jianmin = p("z_jianmin", "张建民", "男", 1955, None, "次子；与建国同父异母。")
    zhao_zg = p("zhao_zg", "赵志刚", "男", 1935, 2018, "李秀兰再婚配偶。")
    zhao_lei = p("zhao_lei", "赵磊", "男", 1970, None, "李秀兰与赵志刚之子；与张氏主干旁支关联。")
    sun_mei = p("sun_mei", "孙梅", "女", 1955, None, "张建国第一任妻子。")
    zhou_min = p("zhou_min", "周敏", "女", 1972, None, "张建国第二任妻子；已离异。")
    chen_fang = p("chen_fang", "陈芳", "女", 1975, None, "张建国现任妻子。")
    z_peng = p("z_peng", "张鹏", "男", 1982, None, "建国与孙梅之子。")
    wangwei_j = p("wangwei_j", "王伟", "男", 1990, None, "建国线：与姑表支系另一「王伟」同名不同人。")
    li_ming = p("li_ming", "李明", "男", 1955, None, "张丽之夫。")
    wangwei_l = p("wangwei_l", "王伟", "男", 1988, None, "张丽线：与堂支「王伟」同名同字，易混淆。")
    z_xiaoyu = p("z_xiaoyu", "张小雨", "女", 1998, None, "建国与周敏之女；父母离异后仍保留生父母亲子边。")
    p_shikao = p("shikao", "张失考", None, None, None, "旧谱未载生年；用于 NULL 字段与检索边界测试。")
    p_nog = p("nog", "佚名", "", 1985, None, "性别未填；用于表单与统计边界。")

    # --- 亲子 ---
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

    # --- 婚姻事实（含离异 / 再婚）---
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
    name_fn: Callable[[int, int], str],
) -> None:
    """
    从 anchor 起向下接一条父链，再侧挂若干叶节点；为每名子女生成母亲与初婚（及低概率离异）。
    count：本函数新增 Person 总行数（含母亲），与场景谱目标人数对齐。
    """
    if count <= 0:
        return

    g = genealogy_id
    chain_len = min(40, max(1, count // 120))
    while chain_len > 0 and (2 * chain_len + 1) > count:
        chain_len -= 1

    if chain_len == 0:
        if count < 2:
            return
        hub = anchor_parent_id
        hub_birth = anchor_birth_year
        first_leaf_birth = min(2022, hub_birth + 22)
        if first_leaf_birth <= hub_birth:
            first_leaf_birth = hub_birth + 22
        h = int(hashlib.md5(g.encode("utf-8")).hexdigest(), 16)
        maiden = MAIDEN_SURNAMES[h % len(MAIDEN_SURNAMES)]
        if maiden == surname:
            maiden = MAIDEN_SURNAMES[1]
        hw_b = mother_birth_for_child(first_leaf_birth, hub_birth, 555)
        wid = str(uuid.uuid4())
        w_persons.writerow(
            [
                wid,
                g,
                bulk_display_name_for_birth(maiden, 910_000, hw_b),
                "女",
                str(hw_b),
                maybe_death_year(hw_b, 1),
                "批量填充·配偶",
                created_at,
            ]
        )
        wed = wedding_year_hetero(hub_birth, hw_b, first_leaf_birth, 42)
        div = bulk_divorce_roll(777, wed, first_leaf_birth)
        w_m.writerow(
            [
                g,
                hub,
                wid,
                str(wed),
                "" if div is None else str(div),
                bulk_marriage_note() + ("·离异" if div is not None else ""),
            ]
        )
        for j in range(count - 1):
            cid = str(uuid.uuid4())
            cb = min(2022, hub_birth + 22 + (j % 18))
            if cb <= hub_birth:
                cb = hub_birth + 22
            w_persons.writerow(
                [
                    cid,
                    g,
                    name_fn(j, cb),
                    "女" if j % 3 == 0 else "男",
                    str(cb),
                    maybe_death_year(cb, j + 3),
                    "批量填充-叶",
                    created_at,
                ]
            )
            w_pc.writerow([g, hub, cid, "father"])
            w_pc.writerow([g, wid, cid, "mother"])
        return

    parent_id = anchor_parent_id
    parent_birth = anchor_birth_year
    remaining = count - (2 * chain_len + 1)

    for i in range(chain_len):
        cid = str(uuid.uuid4())
        cb = parent_birth + 22 + (i % 5)
        w_persons.writerow(
            [
                cid,
                g,
                name_fn(i, cb),
                "男",
                str(cb),
                maybe_death_year(cb, i),
                "批量填充-链式",
                created_at,
            ]
        )
        mb = mother_birth_for_child(cb, parent_birth, 6000 + i * 17)
        mid = str(uuid.uuid4())
        maiden = MAIDEN_SURNAMES[(i + 7) % len(MAIDEN_SURNAMES)]
        if maiden == surname:
            maiden = MAIDEN_SURNAMES[(i + 8) % len(MAIDEN_SURNAMES)]
        w_persons.writerow(
            [
                mid,
                g,
                bulk_display_name_for_birth(maiden, 920_000 + i, mb),
                "女",
                str(mb),
                maybe_death_year(mb, 100_000 + i),
                "批量填充-链式母",
                created_at,
            ]
        )
        wed = wedding_year_hetero(parent_birth, mb, cb, 800 + i)
        div = bulk_divorce_roll(900 + i, wed, cb)
        note = bulk_marriage_note() + ("·离异" if div is not None else "")
        w_m.writerow([g, parent_id, mid, str(wed), "" if div is None else str(div), note])
        w_pc.writerow([g, parent_id, cid, "father"])
        w_pc.writerow([g, mid, cid, "mother"])
        parent_id = cid
        parent_birth = cb

    hub = parent_id
    hub_birth = parent_birth
    first_leaf_birth = min(2022, hub_birth + 22)
    if first_leaf_birth <= hub_birth:
        first_leaf_birth = hub_birth + 22
    h2 = int(hashlib.md5((g + "|h2").encode("utf-8")).hexdigest(), 16)
    maiden_h = MAIDEN_SURNAMES[h2 % len(MAIDEN_SURNAMES)]
    if maiden_h == surname:
        maiden_h = MAIDEN_SURNAMES[5]
    hw_b = mother_birth_for_child(first_leaf_birth, hub_birth, 731)
    wid = str(uuid.uuid4())
    w_persons.writerow(
        [
            wid,
            g,
            bulk_display_name_for_birth(maiden_h, 925_000, hw_b),
            "女",
            str(hw_b),
            maybe_death_year(hw_b, 200_000),
            "批量填充-叶系母",
            created_at,
        ]
    )
    wed_h = wedding_year_hetero(hub_birth, hw_b, first_leaf_birth, 999)
    div_h = bulk_divorce_roll(3333, wed_h, first_leaf_birth)
    w_m.writerow(
        [
            g,
            hub,
            wid,
            str(wed_h),
            "" if div_h is None else str(div_h),
            bulk_marriage_note() + ("·离异" if div_h is not None else "") + "·叶系",
        ]
    )

    for j in range(remaining):
        cid = str(uuid.uuid4())
        cb = min(2022, hub_birth + 20 + (j % 35))
        if cb <= hub_birth:
            cb = hub_birth + 22
        w_persons.writerow(
            [
                cid,
                g,
                name_fn(chain_len + j, cb),
                "女" if j % 3 == 0 else "男",
                str(cb),
                maybe_death_year(cb, chain_len + j),
                "批量填充-叶",
                created_at,
            ]
        )
        w_pc.writerow([g, hub, cid, "father"])
        w_pc.writerow([g, wid, cid, "mother"])
