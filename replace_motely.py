
import os
import re

# Whitelist of sub-namespaces that should NOT be renamed
# Based on search results
PRESERVE_NAMESPACES = {
    "Filters",
    "Analysis",
    "Utils",
    "Desktop",
    "CLI"
}

def should_replace(match):
    # match.group(0) is "Motely."
    # We look ahead to see what follows.
    # Actually, we can just use a regex that captures the lookahead.
    pass

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Regex to find "Motely." not preceded by "class " (to avoid "class Motely" which is already renamed but just in case)
    # And we want to check what follows.
    
    # Simple approach: Replace "Motely." with "MotelyCore."
    # Then revert "MotelyCore.Filters" to "Motely.Filters", etc.
    
    new_content = content.replace("Motely.", "MotelyCore.")
    
    for ns in PRESERVE_NAMESPACES:
        new_content = new_content.replace(f"MotelyCore.{ns}", f"Motely.{ns}")
    
    # Also revert "using Motely;" if it became "using MotelyCore;" (if it had a dot? No, "using Motely;" has no dot)
    # But "using Motely;" (no dot) would NOT be affected by replacement of "Motely.".
    
    # Revert "namespace MotelyCore" if it happened (e.g. "namespace Motely.Filters" -> "namespace MotelyCore.Filters" -> reverted above)
    # "namespace Motely" (no dot) -> unaffected.
    
    if content != new_content:
        print(f"Updating {filepath}")
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)

def main():
    root_dir = r"x:\BalatroSeedOracle\external\Motely\Motely"
    for subdir, dirs, files in os.walk(root_dir):
        for file in files:
            if file.endswith(".cs"):
                process_file(os.path.join(subdir, file))

if __name__ == "__main__":
    main()
