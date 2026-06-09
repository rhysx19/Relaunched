#!/bin/bash
set -e

# Resolve script directory to allow running from anywhere
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "=========================================================="
echo " Classic Launchpad E2E Test Suite Runner                  "
echo "=========================================================="
echo ""

echo "Step 1: Building test project..."
dotnet build "${SCRIPT_DIR}/ClassicLaunchpad.Tests/ClassicLaunchpad.Tests.csproj" -c Release

echo ""
echo "Step 2: Executing tests..."
dotnet test "${SCRIPT_DIR}/ClassicLaunchpad.Tests/ClassicLaunchpad.Tests.csproj" -c Release --no-build

echo ""
echo "=========================================================="
echo " SUCCESS: All tests passed!                               "
echo "=========================================================="
