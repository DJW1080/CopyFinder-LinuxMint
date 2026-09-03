#!/usr/bin/env bash
set -euo pipefail

source_dir=$(cd -P -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
data_home=${XDG_DATA_HOME:-"$HOME/.local/share"}
install_dir="$data_home/CopyFinder"
applications_dir="$data_home/applications"
icons_dir="$data_home/icons/hicolor/48x48/apps"
launcher="$applications_dir/io.github.DJW1080.CopyFinder.desktop"

mkdir -p -- "$install_dir" "$applications_dir" "$icons_dir"
cp -a -- "$source_dir/." "$install_dir/"
chmod +x -- "$install_dir/CopyFinder" "$install_dir/install.sh"

escaped_install_dir=${install_dir//\\/\\\\}
escaped_install_dir=${escaped_install_dir//&/\\&}
escaped_install_dir=${escaped_install_dir//|/\\|}
sed "s|REPLACE_INSTALL_DIR|$escaped_install_dir|g" \
  "$source_dir/io.github.DJW1080.CopyFinder.desktop" > "$launcher"
chmod +x -- "$launcher"

cp -- "$source_dir/Assets/CopyFinder-icon.png" \
  "$icons_dir/io.github.DJW1080.CopyFinder.png"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$applications_dir"
fi

printf 'CopyFinder installed for this user in %s\n' "$install_dir"
printf 'Open the Mint menu and search for CopyFinder.\n'
