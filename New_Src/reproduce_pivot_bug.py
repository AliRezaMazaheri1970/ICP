"""
Reproduction of C# Pivot Logic Bug
"""

def gcd(a, b):
    while b:
        a, b = b, a % b
    return a

def calculate_set_sizes_csharp(rows):
    set_sizes = {}
    grouped_by_solution = {}
    
    # Group by solution
    for r in rows:
        sl = r['SolutionLabel']
        if sl not in grouped_by_solution:
            grouped_by_solution[sl] = []
        grouped_by_solution[sl].append(r)
        
    for sl, group in grouped_by_solution.items():
        element_counts = {}
        for r in group:
            element_counts[r['Element']] = element_counts.get(r['Element'], 0) + 1
        
        counts = list(element_counts.values())
        if counts:
            import functools
            g = functools.reduce(gcd, counts)
            total_rows = len(group)
            if g > 0 and total_rows % g == 0:
                set_sizes[sl] = total_rows // g  # e.g. 6 // 2 = 3
            else:
                set_sizes[sl] = total_rows
        else:
            set_sizes[sl] = 1
            
    return set_sizes

def divide_into_sets_csharp(rows, set_size):
    """Reflecting the CURRENT buggy C# implementation"""
    sets = []
    if set_size <= 0 or len(rows) == 0:
        sets.append(rows)
        return sets

    # BUGGY C# LOGIC:
    # int rowsPerSet = rows.Count / setSize;
    rows_per_set = len(rows) // set_size
    
    print(f"DEBUG C#: Total Rows={len(rows)}, SetSize (param)={set_size}, Calculated RowsPerSet={rows_per_set}")

    for i in range(0, len(rows), rows_per_set):
        chunk = rows[i:i + rows_per_set]
        if chunk:
            sets.append(chunk)
            
    return sets

def divide_into_sets_python_correct(rows, set_size):
    """Reflecting how Python does it (chunk by set_size)"""
    sets = []
    # Python chunks by set_size directly
    # e.g. group_id = pos // set_size
    # which effectively chunks by set_size
    
    print(f"DEBUG PY: Total Rows={len(rows)}, SetSize (param)={set_size}, Using chunk size={set_size}")

    for i in range(0, len(rows), set_size):
        chunk = rows[i:i + set_size]
        if chunk:
            sets.append(chunk)
    return sets

# Mock Data: 3 elements (A, B, C), 2 repeats
# Expectation: 2 sets of 3 rows
rows = [
    {'SolutionLabel': 'S1', 'Element': 'A', 'Val': 10},
    {'SolutionLabel': 'S1', 'Element': 'B', 'Val': 20},
    {'SolutionLabel': 'S1', 'Element': 'C', 'Val': 30},
    {'SolutionLabel': 'S1', 'Element': 'A', 'Val': 11},
    {'SolutionLabel': 'S1', 'Element': 'B', 'Val': 21},
    {'SolutionLabel': 'S1', 'Element': 'C', 'Val': 31},
]

print("="*60)
print("TESTING C# LOGIC BUG")
print("="*60)

# 1. Calculate Set Size
set_sizes = calculate_set_sizes_csharp(rows)
calculated_set_size = set_sizes['S1']
print(f"Calculated Set Size (Expected Rows Per Set): {calculated_set_size}") # Should be 3

# 2. Divide into Sets (Buggy)
print("\n--- Running Buggy C# Logic ---")
sets_csharp = divide_into_sets_csharp(rows, calculated_set_size)
print(f"Resulting Sets count: {len(sets_csharp)}")
for i, s in enumerate(sets_csharp):
    print(f"Set {i}: {[r['Element'] for r in s]}")

# 3. Divide into Sets (Correct)
print("\n--- Running Correct Python Logic ---")
sets_python = divide_into_sets_python_correct(rows, calculated_set_size)
print(f"Resulting Sets count: {len(sets_python)}")
for i, s in enumerate(sets_python):
    print(f"Set {i}: {[r['Element'] for r in s]}")

print("\nCONCLUSION:")
if len(sets_csharp) != len(sets_python):
    print("MISMATCH! C# logic creates wrong number of sets.")
else:
    print("Match (unexpected). Check logic.")
