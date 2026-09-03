#!/usr/bin/env bash
set -euo pipefail

fixture_dir=$(mktemp -d -t copyfinder-smoke-XXXXXX)
mkdir -p -- "$fixture_dir/first" "$fixture_dir/second"
printf '%s' 'alpha duplicate' > "$fixture_dir/first/alpha.txt"
cp -- "$fixture_dir/first/alpha.txt" "$fixture_dir/second/alpha-copy.txt"
printf '%s' 'beta duplicate' > "$fixture_dir/first/beta.txt"
cp -- "$fixture_dir/first/beta.txt" "$fixture_dir/second/beta-copy.txt"
printf '%s' 'unique' > "$fixture_dir/unique.txt"
printf '%s' 'excluded duplicate' > "$fixture_dir/first/ignored.tmp"
cp -- "$fixture_dir/first/ignored.tmp" "$fixture_dir/second/ignored.tmp"
cp -- "$fixture_dir/first/alpha.txt" "$fixture_dir/.hidden-alpha.txt"

printf 'Fixture: %s\n' "$fixture_dir"
printf 'Expected with hidden files and tmp excluded: 2 groups, 2 duplicate files\n'
