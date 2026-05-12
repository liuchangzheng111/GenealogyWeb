# -*- coding: utf-8 -*-
"""常见姓名池：用于万级数据生成时「像真名」且全局唯一（后缀序号）。

按出生年微调名池权重，避免「清代生人叫梓涵」这类明显违和（仍非真实人口统计，仅观感更自然）。
"""

GIVEN_NAMES = [
    "志强", "秀英", "建国", "桂花", "文华", "秀兰", "明华", "淑芬", "伟", "敏",
    "静", "磊", "洋", "艳", "军", "杰", "娟", "涛", "丽", "勇",
    "霞", "超", "秀云", "海燕", "冬梅", "雪梅", "玉兰", "桂英", "凤英", "红梅",
    "俊", "鹏", "飞", "浩", "宇", "欣", "怡", "琳", "婷", "颖",
    "浩宇", "梓涵", "子轩", "思琪", "雨泽", "梦瑶", "博文", "佳怡", "晨曦", "可欣",
    "承志", "家豪", "美玲", "淑华", "国栋", "春梅", "秋菊", "夏荷", "冬雪", "青松",
    "柏林", "竹君", "梅兰", "菊芳", "莲香", "荷清", "柳青", "杨帆", "枫林", "桐华",
    "云龙", "凤舞", "鹤鸣", "鸿志", "燕归", "莺啼", "蝶恋", "蜂舞", "蝉鸣", "雁南",
]

# 偏旧谱/民国至改开前常见用字（与 GIVEN_NAMES 有重叠，整体更「老」）
GIVEN_NAMES_TRADITIONAL = [
    "德厚", "秀兰", "桂花", "文华", "淑芬", "志强", "秀英", "建国", "明华", "秀云",
    "春梅", "秋菊", "玉兰", "桂英", "凤英", "红梅", "国栋", "承志", "柏林", "竹君",
    "梅兰", "菊芳", "莲香", "荷清", "青松", "云龙", "鸿志", "燕归", "雁南", "鹤鸣",
]

# 偏 1990 后至 Z 世代网络常见风格（与主池尾部重叠）
GIVEN_NAMES_MODERN = [
    "梓涵", "子轩", "思琪", "雨泽", "梦瑶", "浩宇", "博文", "佳怡", "晨曦", "可欣",
    "家豪", "欣怡", "俊宇", "诗涵", "奕辰", "一诺", "沐阳", "若曦", "宸熙", "语桐",
]

SURNAMES_LI_BRANCH = "李"


def _name_pool_for_birth_year(birth_year: int | None) -> list[str]:
    if birth_year is None:
        return GIVEN_NAMES
    if birth_year < 1950:
        return GIVEN_NAMES_TRADITIONAL
    if birth_year < 1990:
        return GIVEN_NAMES
    return GIVEN_NAMES_MODERN + GIVEN_NAMES[-20:]


def bulk_display_name(surname: str, index: int) -> str:
    """同一族谱内唯一、可读：姓 + 常见名 + 序号避免撞名。"""
    g = GIVEN_NAMES[index % len(GIVEN_NAMES)]
    return f"{surname}{g}{index // len(GIVEN_NAMES):04d}"


def bulk_display_name_for_birth(surname: str, index: int, birth_year: int | None) -> str:
    """按出生年选名池，再拼序号，减少跨时代违和感。"""
    pool = _name_pool_for_birth_year(birth_year)
    g = pool[index % len(pool)]
    return f"{surname}{g}{index // max(len(pool), 1):04d}"


def maybe_death_year(birth_year: int | None, index: int) -> str:
    """
    部分人物填写卒年：满足 BirthYear <= DeathYear；无出生年则不填卒年。
    确定性规则，便于回归对比同一 index 行为一致。
    """
    if birth_year is None:
        return ""
    if birth_year > 2010:
        return ""
    # 约 1/4 有卒年；寿命 68–96 岁
    if (index + birth_year) % 4 != 0:
        return ""
    span = 68 + ((index * 17 + birth_year) % 29)
    return str(birth_year + span)
