#!/usr/bin/env bash
set -euo pipefail

echo "Running EF Core database migrations..."
dotnet Kromic.Api.dll --migrate-only
echo "EF Core database migrations completed."
