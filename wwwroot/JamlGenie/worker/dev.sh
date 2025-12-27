#!/bin/bash
# Quick dev script for local testing

echo "🧞 Starting JamlGenie Worker locally..."
echo ""
echo "Make sure you've run: wrangler login"
echo ""

cd "$(dirname "$0")"
wrangler dev
