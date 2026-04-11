#!/usr/bin/env python3
"""Generate C# pattern table from the JSON extracted from mintlemon pickle."""
import json

INPUT = r'c:\Users\lener\Desktop\code\csharp\hizli_yazici\tmp\pattern_table.json'
OUTPUT = r'c:\Users\lener\Desktop\code\csharp\hizli_yazici\Services\TurkishPatternTable.cs'

with open(INPUT, 'r', encoding='utf-8') as f:
    pattern_table = json.load(f)

print(f"Loaded {len(pattern_table)} characters")
for k, v in pattern_table.items():
    print(f"  '{k}': {len(v)} patterns")

cs = []
cs.append("// AUTO-GENERATED - Turkish Deasciifier Pattern Table")
cs.append("// Based on Deniz Yüret's turkish-mode algorithm")
cs.append("// Source: mintlemon library (DeasciifierBuiltin)")
cs.append("using System.Collections.Generic;")
cs.append("")
cs.append("namespace YaziciCS.Services")
cs.append("{")
cs.append("    public static class TurkishPatternTable")
cs.append("    {")
cs.append("        public static readonly Dictionary<string, Dictionary<string, int>> Table;")
cs.append("")
cs.append("        static TurkishPatternTable()")
cs.append("        {")
cs.append("            Table = new Dictionary<string, Dictionary<string, int>>();")

char_keys = list(pattern_table.keys())
for char_key in char_keys:
    patterns = pattern_table[char_key]
    escaped_key = char_key.replace("\\", "\\\\").replace('"', '\\"')
    cs.append(f'            var d_{char_key} = new Dictionary<string, int>({len(patterns)});')
    
    for pattern, rank in patterns.items():
        escaped = pattern.replace("\\", "\\\\").replace('"', '\\"')
        cs.append(f'            d_{char_key}["{escaped}"] = {rank};')
    
    cs.append(f'            Table["{escaped_key}"] = d_{char_key};')
    cs.append("")

cs.append("        }")
cs.append("    }")
cs.append("}")

with open(OUTPUT, 'w', encoding='utf-8') as f:
    f.write('\n'.join(cs))

total = sum(len(v) for v in pattern_table.values())
print(f"\nWrote {OUTPUT}")
print(f"Total patterns: {total}")
