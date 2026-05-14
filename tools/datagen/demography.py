# -*- coding: utf-8 -*-
"""
简化人口学规则（确定性、可复现），用于批量生成配偶与母亲出生年、初婚年。
不追求统计拟合真实人口，只保证：库触发器、CHECK、与「男父/女母」一致。
"""

from __future__ import annotations

MAIDEN_SURNAMES = [
    "王",
    "刘",
    "陈",
    "杨",
    "黄",
    "周",
    "吴",
    "赵",
    "孙",
    "马",
    "朱",
    "胡",
    "郭",
    "林",
    "何",
    "罗",
    "梁",
    "宋",
    "郑",
    "谢",
]


def _clamp(x: int, lo: int, hi: int) -> int:
    return max(lo, min(hi, x))


def mother_birth_for_child(child_birth: int, father_birth: int, salt: int) -> int:
    """
    母亲出生年：严格早于子女；初育年龄约 22–36；多数为「夫略长于妻」。
    """
    mom_age = 22 + ((salt * 17 + child_birth) % 15)  # 22..36
    m = child_birth - mom_age
    # 母亲出生年围绕父亲小幅波动，但必须明显早于子女。
    lower_bound = father_birth - 6
    upper_bound = min(father_birth + 6, child_birth - 18)
    if upper_bound < lower_bound:
        upper_bound = child_birth - 18
    if m < lower_bound:
        m = lower_bound
    if m > upper_bound:
        m = upper_bound
    if m >= child_birth:
        m = child_birth - 18
    return m


def wedding_year_hetero(
    male_birth: int,
    female_birth: int,
    first_child_birth: int | None,
    salt: int,
) -> int:
    """初婚历年年份：双方成年后；若有首胎，则婚年早于首胎出生年。"""
    adult_floor = max(male_birth, female_birth) + 20
    if first_child_birth is None:
        return adult_floor + ((salt * 7) % 6)

    wed_hi = first_child_birth - 1
    wed_lo = max(adult_floor, min(male_birth, female_birth) + 18)
    if wed_lo > wed_hi:
        wed_lo = wed_hi
    span = max(1, wed_hi - wed_lo + 1)
    return wed_lo + ((salt * 13) % span)


def bulk_marriage_note() -> str:
    return "批量生成·初婚"


def bulk_divorce_roll(salt: int, wed_year: int, last_child_birth: int | None) -> int | None:
    """约 1/47 概率离异（历年年份 >= 初婚年）。"""
    if (salt % 47) != 0:
        return None
    div = wed_year + 10 + ((salt >> 3) % 20)
    if last_child_birth is not None:
        div = max(div, last_child_birth + 1)
    return div
