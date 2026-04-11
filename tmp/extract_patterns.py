#!/usr/bin/env python3
"""Extract pattern table from Python deasciifier source and output as C# code."""
import sys
import re
import json

SOURCE_FILE = r'C:\Users\lener\.gemini\antigravity\brain\6640f47f-642c-4767-b7fa-369b620f2395\.system_generated\steps\22\content.md'
OUTPUT_FILE = r'c:\Users\lener\Desktop\code\csharp\hizli_yazici\Services\TurkishPatternTable.cs'

with open(SOURCE_FILE, 'r', encoding='utf-8') as f:
    content = f.read()

# Remove markdown line numbers (format: "123: actual content")
lines = content.split('\n')
cleaned_lines = []
for line in lines:
    cleaned = re.sub(r'^\d+:\s?', '', line)
    cleaned_lines.append(cleaned)
content = '\n'.join(cleaned_lines)

# Find the pattern table definition
start_marker = "turkish_pattern_table = {"
end_marker = "turkish_context_size = 10"

start_idx = content.find(start_marker)
end_idx = content.find(end_marker)

if start_idx == -1 or end_idx == -1:
    print(f"Could not find markers. start={start_idx}, end={end_idx}")
    sys.exit(1)

table_text = content[start_idx + len("turkish_pattern_table = "):end_idx].strip()

# Clean up: remove trailing commas, whitespace issues
# The table is a Python dict literal: {u'c': {...}, u'g': {...}, ...}

# Replace Python unicode prefix u'...' with '...'
# Use lookbehind to avoid eating the 'u' character in u'u' (dict key)
table_text = re.sub(r"(?<!\w)u'", "'", table_text)
table_text = re.sub(r'(?<!\w)u"', '"', table_text)

# Convert Python dict to valid JSON-like format
# Replace single quotes with double quotes
# But first handle escaped single quotes
table_text = table_text.replace("\\'", "__ESCAPED_QUOTE__")
table_text = table_text.replace("'", '"')
table_text = table_text.replace("__ESCAPED_QUOTE__", "'")

# Remove trailing commas before closing braces
table_text = re.sub(r',\s*}', '}', table_text)

# The dict uses True/False - not needed since we only have string:int pairs

# Try to parse as JSON
try:
    pattern_table = json.loads(table_text)
    print(f"Successfully parsed pattern table with {len(pattern_table)} characters")
    for key, val in pattern_table.items():
        print(f"  '{key}': {len(val)} patterns")
except json.JSONDecodeError as e:
    # Debug: find where the error is
    print(f"JSON parse error: {e}")
    # Find the error location
    error_pos = e.pos if hasattr(e, 'pos') else -1
    if error_pos > 0:
        print(f"Context around error (pos {error_pos}):")
        start = max(0, error_pos - 100)
        end = min(len(table_text), error_pos + 100)
        print(repr(table_text[start:end]))
    sys.exit(1)

# Now generate C# code
cs_lines = []
cs_lines.append("// AUTO-GENERATED from Deniz Yüret / Emre Sevinç Turkish Deasciifier")
cs_lines.append("// Pattern table for context-based Turkish character correction")
cs_lines.append("using System.Collections.Generic;")
cs_lines.append("")
cs_lines.append("namespace YaziciCS.Services")
cs_lines.append("{")
cs_lines.append("    public static class TurkishPatternTable")
cs_lines.append("    {")
cs_lines.append("        public static readonly Dictionary<string, Dictionary<string, int>> Table = new Dictionary<string, Dictionary<string, int>>()")
cs_lines.append("        {")

for char_key, patterns in pattern_table.items():
    cs_lines.append(f'            {{"{char_key}", new Dictionary<string, int>()')
    cs_lines.append('            {')
    
    items = list(patterns.items())
    for i, (pattern, rank) in enumerate(items):
        # Escape the pattern for C# string
        escaped = pattern.replace("\\", "\\\\").replace('"', '\\"')
        comma = "," if i < len(items) - 1 else ""
        cs_lines.append(f'                {{"{escaped}", {rank}}}{comma}')
    
    # Close the inner dict
    is_last_char = char_key == list(pattern_table.keys())[-1]
    cs_lines.append('            }}' + ('' if is_last_char else ','))

cs_lines.append("        };")
cs_lines.append("    }")
cs_lines.append("}")

# Write C# file
with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
    f.write('\n'.join(cs_lines))

print(f"\nWrote C# pattern table to {OUTPUT_FILE}")
total_patterns = sum(len(v) for v in pattern_table.values())
print(f"Total patterns: {total_patterns}")
