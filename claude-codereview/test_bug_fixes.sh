#!/bin/bash
# ============================================================================
# MOTELY MOTELYJON BUG FIX VALIDATION SCRIPT
# ============================================================================
# Run this script after applying the bug fixes to validate they work

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Motely MotelyJson Bug Fix Validation${NC}"
echo -e "${BLUE}========================================${NC}"
echo

# Change to the Motely directory
cd "X:/BalatroSeedOracle/external/Motely/Motely" || {
    echo -e "${RED}❌ Failed to change to Motely directory${NC}"
    exit 1
}

# Build the project first
echo -e "${YELLOW}🔨 Building Motely...${NC}"
dotnet build -c Release --nologo -v minimal
if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Build failed${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Build successful${NC}"
echo

# Test data - each test has: seed, config_file, expected_item, description
declare -a test_cases=(
    "ALEEB|test-boss|TheArm|Boss filter state management (Ante 2)"
    "ALEEB|test-planet-shop|Saturn|Planet in shop consumables (Ante 2, Slot 0)"
    "ALEEB|test-tarot-shop|TheEmpress|Tarot in shop consumables (Ante 1, Slot 2)"
    "ALEEB|test-blueprint-slot7|Blueprint|Joker at extended slot (Ante 2, Slot 7)"
)

# Counters
passed=0
total=${#test_cases[@]}

echo -e "${BLUE}Running $total test cases...${NC}"
echo

# Function to run a single test
run_test() {
    local IFS='|'
    local test_data=($1)
    local seed="${test_data[0]}"
    local config="${test_data[1]}"
    local expected="${test_data[2]}"
    local description="${test_data[3]}"
    
    echo -e "${YELLOW}Testing: $description${NC}"
    echo -e "  Seed: $seed, Config: $config, Expected: $expected"
    
    # Create a temporary file for output
    local temp_file=$(mktemp)
    
    # Run the test and capture output
    timeout 30s dotnet run -c Release -- --seed "$seed" --json "$config" --debug > "$temp_file" 2>&1
    local exit_code=$?
    
    # Check if the test completed successfully
    if [ $exit_code -eq 124 ]; then
        echo -e "  ${RED}❌ TIMEOUT: Test took longer than 30 seconds${NC}"
        rm "$temp_file"
        return 1
    elif [ $exit_code -ne 0 ]; then
        echo -e "  ${RED}❌ RUNTIME ERROR: Exit code $exit_code${NC}"
        echo -e "  ${RED}   Output:${NC}"
        sed 's/^/    /' "$temp_file"
        rm "$temp_file"
        return 1
    fi
    
    # Check if the expected item was found
    if grep -q "Seeds matched: [1-9]" "$temp_file"; then
        echo -e "  ${GREEN}✅ PASS: Found matching seed${NC}"
        
        # Show debug output about what was found
        if grep -q "MATCH\|Found" "$temp_file"; then
            echo -e "  ${GREEN}   Debug matches:${NC}"
            grep "MATCH\|Found" "$temp_file" | sed 's/^/    /'
        fi
        
        rm "$temp_file"
        return 0
    else
        echo -e "  ${RED}❌ FAIL: No matching seeds found${NC}"
        
        # Show some debug output to help diagnose
        echo -e "  ${RED}   Search output:${NC}"
        grep -E "(Seeds searched|Seeds matched|ERROR|Exception)" "$temp_file" | sed 's/^/    /'
        
        # Show any debug output that might be helpful
        if grep -q "DEBUG" "$temp_file"; then
            echo -e "  ${RED}   First few debug lines:${NC}"
            grep "DEBUG" "$temp_file" | head -5 | sed 's/^/    /'
        fi
        
        rm "$temp_file"
        return 1
    fi
}

# Run all tests
for test_case in "${test_cases[@]}"; do
    if run_test "$test_case"; then
        ((passed++))
    fi
    echo
done

# Summary
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Test Results Summary${NC}"
echo -e "${BLUE}========================================${NC}"

if [ $passed -eq $total ]; then
    echo -e "${GREEN}🎉 ALL TESTS PASSED: $passed/$total${NC}"
    echo -e "${GREEN}✅ Bug fixes are working correctly!${NC}"
    exit 0
else
    failed=$((total - passed))
    echo -e "${RED}❌ SOME TESTS FAILED: $passed/$total passed, $failed failed${NC}"
    echo -e "${YELLOW}📝 Check the failed tests above for debugging information${NC}"
    exit 1
fi
