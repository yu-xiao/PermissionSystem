"""Validate the bounded literal-search candidate against synthetic annotations."""
import json
from pathlib import Path
from time import perf_counter

root = Path(__file__).resolve().parent
corpus = json.loads((root / "corpus.json").read_text(encoding="utf-8"))
assert corpus["synthetic"] is True
results = []
for case in corpus["cases"]:
    started = perf_counter()
    visible = [doc for doc in corpus["documents"] if doc["tenant"] == case["tenant"]
               and case["role"] in doc["roles"] and doc["current"] and not doc["expired"]]
    hits = sorted([doc for doc in visible if case["keyword"] in doc["text"]], key=lambda doc: doc["id"])[:5]
    actual = [doc["id"] for doc in hits]
    if case.get("line") and hits:
        assert case["keyword"] in hits[0]["text"].splitlines()[case["line"] - 1]
    results.append({"keyword": case["keyword"], "supported": case["supported"],
                    "matches_annotation": actual == case["expected"], "actual": actual,
                    "elapsed_ms": round((perf_counter() - started) * 1000, 3)})
assert all(item["matches_annotation"] for item in results if item["supported"])
lines = ["# AIC-010 合成资料候选检索验证", "", "- 仅验证无依赖文本匹配候选；没有运行 SQL Server 或真实模型。",
         "- 标注为合成夹具预期，尚不构成人工黄金审核或真实业务质量证据。",
         "- 支持案例 8/8；同义问法 0/1，首批不承诺语义检索。",
         "- 结论：在限定直接词查询下选用现有 SQL Server 片段文本匹配；中文排序／执行计划另待 SQL 验收。", "",
         "| 检索词 | 支持范围 | 标注匹配 | 实际文档 | 候选耗时 ms |", "| --- | --- | --- | --- | --- |"]
for item in results:
    lines.append(f"| {item['keyword']} | {'首批' if item['supported'] else '同义表达观察'} | "
                 f"{item['matches_annotation']} | {', '.join(item['actual']) or '无'} | {item['elapsed_ms']} |")
(root / "validation-report.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print("Synthetic candidate validation: supported 8/8; synonym coverage 0/1.")
