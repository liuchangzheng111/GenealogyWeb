# -*- coding: utf-8 -*-
"""常见姓名池：用于万级数据生成时「像真名」且全局唯一（后缀序号）。

按出生年微调名池权重，避免「清代生人叫梓涵」这类明显违和（仍非真实人口统计，仅观感更自然）。
"""
from datetime import datetime

_BASIC_GIVEN_NAMES = [
    "志强", "秀英", "建国", "桂花", "文华", "秀兰", "明华", "淑芬", "伟", "敏",
    "静", "磊", "洋", "艳", "军", "杰", "娟", "涛", "丽", "勇",
    "霞", "超", "秀云", "海燕", "冬梅", "雪梅", "玉兰", "桂英", "凤英", "红梅",
    "俊", "鹏", "飞", "浩", "宇", "欣", "怡", "琳", "婷", "颖",
    "浩宇", "梓涵", "子轩", "思琪", "雨泽", "梦瑶", "博文", "佳怡", "晨曦", "可欣",
    "承志", "家豪", "美玲", "淑华", "国栋", "春梅", "秋菊", "夏荷", "冬雪", "青松",
    "柏林", "竹君", "梅兰", "菊芳", "莲香", "荷清", "柳青", "杨帆", "枫林", "桐华",
    "云龙", "凤舞", "鹤鸣", "鸿志", "燕归", "莺啼", "蝶恋", "蜂舞", "蝉鸣", "雁南",
]

_TRADITIONAL_BASE = [
    "德厚", "德富", "德明", "德义", "德仁", "德安", "德志", "德才", "德胜", "德兴",
    "秀兰", "秀英", "桂花", "文华", "淑芬", "志强", "建国", "明华", "秀云", "春梅",
    "秋菊", "玉兰", "桂英", "凤英", "红梅", "国栋", "承志", "柏林", "竹君", "梅兰",
    "菊芳", "莲香", "荷清", "青松", "云龙", "鸿志", "燕归", "雁南", "鹤鸣",
]

_MODERN_BASE = [
    "梓涵", "子轩", "思琪", "雨泽", "梦瑶", "浩宇", "博文", "佳怡", "晨曦", "可欣",
    "家豪", "欣怡", "俊宇", "诗涵", "奕辰", "一诺", "沐阳", "若曦", "宸熙", "语桐",
    "瑾萱", "子墨", "若汐", "星河", "亦辰", "昕然", "嘉言", "书恒", "亦凡", "知夏",
]

_TRADITIONAL_SECOND = ["强", "富", "华", "明", "平", "安", "德", "义", "才", "兴", "杰", "忠", "仁", "礼", "信", "勇", "成", "建", "昌", "康", "良", "顺", "贵", "福", "祥", "和", "盛", "隆", "太", "启"]
_MODERN_SECOND = ["涵", "轩", "辰", "泽", "宇", "然", "怡", "希", "桐", "瑶", "昊", "言", "墨", "宁", "晨", "歌", "一", "诺", "安", "汐", "川", "宸", "屿", "星", "芮", "知", "屿", "淇", "洛", "朵"]
_EXTRA_SECOND = ["清", "山", "林", "风", "云", "月", "川", "海", "松", "竹", "梅", "兰", "菊", "荷", "若", "初", "归", "远", "宁", "安", "予", "知", "见", "书", "野", "川", "屿", "歌", "夏", "秋"]


def _expand_pool(base: list[str], second_chars: list[str], target_size: int) -> list[str]:
    pool: list[str] = []
    seen: set[str] = set()

    for name in base:
        if name not in seen:
            seen.add(name)
            pool.append(name)

    for first in base:
        if len(pool) >= target_size:
            break
        stem = first[:1]
        for second in second_chars:
            candidate = stem + second
            if candidate not in seen:
                seen.add(candidate)
                pool.append(candidate)
            if len(pool) >= target_size:
                break

    if len(pool) < target_size:
        for first in base:
            if len(pool) >= target_size:
                break
            if len(first) < 2:
                continue
            for second in second_chars:
                candidate = first[:2] + second
                if candidate not in seen:
                    seen.add(candidate)
                    pool.append(candidate)
                if len(pool) >= target_size:
                    break

    if len(pool) < target_size:
        for first in base:
            if len(pool) >= target_size:
                break
            for second in second_chars:
                for third in second_chars:
                    candidate = first[:1] + second + third
                    if candidate not in seen:
                        seen.add(candidate)
                        pool.append(candidate)
                    if len(pool) >= target_size:
                        break
                if len(pool) >= target_size:
                    break

    if len(pool) < target_size:
        for first in base:
            if len(pool) >= target_size:
                break
            for second in second_chars:
                for third in second_chars:
                    candidate = first[:1] + second + third
                    if candidate not in seen:
                        seen.add(candidate)
                        pool.append(candidate)
                    if len(pool) >= target_size:
                        break
                if len(pool) >= target_size:
                    break

    return pool


GIVEN_NAMES = _expand_pool(_BASIC_GIVEN_NAMES, _MODERN_SECOND + _TRADITIONAL_SECOND + _EXTRA_SECOND, 5000)

# 偏旧谱/民国至改开前常见用字（与 GIVEN_NAMES 有重叠，整体更「老」）
GIVEN_NAMES_TRADITIONAL = _expand_pool(_TRADITIONAL_BASE, _TRADITIONAL_SECOND + _EXTRA_SECOND, 4000)

# 偏 1990 后至 Z 世代网络常见风格（与主池尾部重叠）
GIVEN_NAMES_MODERN = _expand_pool(_MODERN_BASE, _MODERN_SECOND + _EXTRA_SECOND, 4000)

SURNAMES_LI_BRANCH = "李"


def _name_pool_for_birth_year(birth_year: int | None) -> list[str]:
    if birth_year is None:
        return GIVEN_NAMES
    if birth_year < 1950:
        return GIVEN_NAMES_TRADITIONAL
    if birth_year < 1990:
        return GIVEN_NAMES
    return GIVEN_NAMES_MODERN + GIVEN_NAMES[-600:]


def bulk_display_name(surname: str, index: int) -> str:
    """同一族谱内可读：姓 + 常见名。"""
    pool = GIVEN_NAMES
    g = pool[(index * 97 + len(surname)) % len(pool)]
    return f"{surname}{g}"


def bulk_display_name_for_birth(surname: str, index: int, birth_year: int | None) -> str:
    """按出生年选名池，再拼序号，减少跨时代违和感。"""
    pool = _name_pool_for_birth_year(birth_year)
    g = pool[(index * 97 + (birth_year or 0)) % len(pool)]
    return f"{surname}{g}"


def maybe_death_year(birth_year: int | None, index: int) -> str:
    """
    部分人物填写卒年：满足 BirthYear <= DeathYear；无出生年则不填卒年。
    确定性规则，便于回归对比同一 index 行为一致。
    """
    if birth_year is None:
        return ""
    current_year = datetime.now().year
    if birth_year > current_year:
        return ""
    if birth_year > 2010:
        return ""
    # 大部分人物有卒年；寿命 68–96 岁
    if (index + birth_year) % 10 == 0:
        return ""
    span = 68 + ((index * 17 + birth_year) % 29)
    death_year = min(current_year, birth_year + span)
    if death_year > current_year:
        return ""
    return str(death_year)
