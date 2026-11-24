"""Prepare a local import preview from Shark Accounting UTF-16 tab-separated exports."""

import argparse
import csv
import json
import uuid
from collections import defaultdict
from datetime import date, datetime
from decimal import Decimal
from pathlib import Path

CUTOFF = date(2026, 11, 1)
FIELDS = ("日期", "收支类型", "类别", "账户", "金额", "备注")
CENT = Decimal("0.01")


def collect_expenses(paths):
    monthly = defaultdict(lambda: [Decimal(0), 0])
    seen = set()
    removed_duplicates = 0
    excluded_expenses = 0
    excluded_income = 0

    for path in paths:
        with path.open(encoding="utf-16", newline="") as stream:
            reader = csv.DictReader(stream, delimiter="\t")
            if reader.fieldnames != list(FIELDS):
                raise ValueError(f"{path.name}: 无法识别表头")

            for row in reader:
                try:
                    if set(row) != set(FIELDS) or any(value is None for value in row.values()):
                        raise ValueError("字段数量有误")
                    business_date = datetime.strptime(row["日期"], "%Y年%m月%d日").date()
                    kind = row["收支类型"]
                    if kind == "收入":
                        excluded_income += 1
                        continue
                    if kind != "支出":
                        raise ValueError("未知的收支类型")
                    if business_date >= CUTOFF:
                        excluded_expenses += 1
                        continue

                    category = row["类别"].strip()
                    amount = Decimal(row["金额"])
                    if not category or not amount.is_finite() or amount <= 0 or amount != amount.quantize(CENT):
                        raise ValueError("分类或金额有误")

                    identity = (business_date, category, row["账户"], amount, row["备注"])
                    if identity in seen:
                        removed_duplicates += 1
                        continue

                    seen.add(identity)
                    bucket = monthly[business_date.replace(day=1), category]
                    bucket[0] += amount
                    bucket[1] += 1
                except (ValueError, ArithmeticError) as error:
                    raise ValueError(f"{path.name} 第 {reader.line_num} 行: {error}") from error

    return monthly, removed_duplicates, excluded_expenses, excluded_income


def write_preview(monthly, output_dir):
    if not monthly:
        raise ValueError("没有符合条件的支出")
    if len(monthly) > 1000 or any(amount > 1_000_000_000 for amount, _ in monthly.values()):
        raise ValueError("超出导入接口的批次或金额上限")

    output_dir.mkdir(parents=True, exist_ok=True)
    rows = sorted(monthly.items())
    with (output_dir / "导入预览.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["月份", "原始支出分类", "支出金额", "来源笔数", "现金变动"])
        for ((month, category), (amount, count)) in rows:
            writer.writerow([month.strftime("%Y-%m"), category, f"{amount:.2f}", count, "0.00"])

    payload_rows = [
        {"month": month.isoformat(), "category": category, "amount": float(amount)}
        for ((month, category), (amount, _)) in rows
    ]
    fingerprint = json.dumps(payload_rows, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    request_id = uuid.uuid5(uuid.NAMESPACE_URL, f"lifeup:shark-monthly-expenses:{fingerprint}")
    payload = {"requestId": str(request_id), "rows": payload_rows}
    (output_dir / "导入请求.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("exports", nargs="+", type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    monthly, duplicates, excluded, income = collect_expenses(args.exports)
    write_preview(monthly, args.output_dir)
    total = sum(amount for amount, _ in monthly.values())
    count = sum(count for _, count in monthly.values())
    print(f"{len(monthly)} 条汇总，来自 {count} 笔支出，总额 ¥{total:,.2f}，现金变动 ¥0.00")
    print(f"去除重复 {duplicates} 笔，跳过截止月份起的支出 {excluded} 笔、收入 {income} 笔")


if __name__ == "__main__":
    main()
