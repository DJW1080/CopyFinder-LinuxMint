#!/usr/bin/env bash
set -euo pipefail

repo_dir=$(cd -P -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
publish_dir="$repo_dir/publish"
output_dir="$publish_dir/CopyFinder-linux-x64"
archive_name="CopyFinder-v3.0.0-linux-x64.tar.gz"

# The fixed output path is deliberately cleared so stale runtime files cannot
# leak into a release. It never operates outside this repository's publish dir.
rm -rf -- "$output_dir"
mkdir -p -- "$output_dir"

dotnet publish "$repo_dir/src/CopyFinder.App/CopyFinder.App.csproj" \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output "$output_dir"

cp -- "$repo_dir/packaging/install.sh" "$output_dir/install.sh"
cp -- "$repo_dir/packaging/io.github.DJW1080.CopyFinder.desktop" \
  "$output_dir/io.github.DJW1080.CopyFinder.desktop"
cp -- "$repo_dir/README.md" "$repo_dir/INSTALL.md" "$repo_dir/LICENSE" "$output_dir/"
chmod +x -- "$output_dir/CopyFinder" "$output_dir/install.sh"

tar --sort=name --mtime='@0' --owner=0 --group=0 --numeric-owner \
  -C "$output_dir" -czf "$publish_dir/$archive_name" .
(
  cd -- "$publish_dir"
  sha256sum "$archive_name" > "$archive_name.sha256"
)

printf 'Created %s\n' "$publish_dir/$archive_name"
printf 'Created %s\n' "$publish_dir/$archive_name.sha256"
