#!/bin/sh
# Installs Surl, the server-side mate of curl, on Linux or macOS from its GitHub release.
#
#   curl -fsSL https://raw.githubusercontent.com/StewartScottRogers/Surl/master/install.sh | sh
#
# Environment:
#   SURL_VERSION      release tag to install, e.g. v0.1.0 (default: the newest release,
#                     pre-releases included)
#   SURL_INSTALL_DIR  where the binary goes (default: $HOME/.surl/bin)
#
# The binary is named `surl` and is installed into a directory of its own; put that
# directory on PATH to run it by name. No release has been published yet; until one is,
# this script stops at "no release found". See DOWNLOAD.md.
set -eu

repo="StewartScottRogers/Surl"
version="${SURL_VERSION:-latest}"
dir="${SURL_INSTALL_DIR:-$HOME/.surl/bin}"

case "$(uname -s)" in
  Linux)  os=linux ;;
  Darwin) os=osx ;;
  *) echo "install.sh: unsupported operating system $(uname -s); see DOWNLOAD.md" >&2; exit 1 ;;
esac
case "$(uname -m)" in
  x86_64|amd64)  arch=x64 ;;
  aarch64|arm64) arch=arm64 ;;
  *) echo "install.sh: unsupported processor $(uname -m); see DOWNLOAD.md" >&2; exit 1 ;;
esac
package="surl-$os-$arch.tar.gz"

fetch() {
  if command -v curl >/dev/null 2>&1; then curl -fsSL -o "$2" "$1"
  elif command -v wget >/dev/null 2>&1; then wget -qO "$2" "$1"
  else echo "install.sh: needs curl or wget to download" >&2; exit 1
  fi
}

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# releases/latest skips pre-releases, so ask the API for the newest release of any kind.
if [ "$version" = latest ]; then
  fetch "https://api.github.com/repos/$repo/releases?per_page=1" "$tmp/releases.json"
  version="$(sed -n 's/.*"tag_name": *"\([^"]*\)".*/\1/p' "$tmp/releases.json" | head -n 1)"
  if [ -z "$version" ]; then
    echo "install.sh: no release found at https://github.com/$repo/releases" >&2
    exit 1
  fi
fi
base="https://github.com/$repo/releases/download/$version"

echo "Downloading $package ($version)"
fetch "$base/$package" "$tmp/$package"
fetch "$base/SHA256SUMS" "$tmp/SHA256SUMS"

expected="$(grep " $package\$" "$tmp/SHA256SUMS" | cut -d' ' -f1)"
if command -v sha256sum >/dev/null 2>&1; then
  actual="$(sha256sum "$tmp/$package" | cut -d' ' -f1)"
else
  actual="$(shasum -a 256 "$tmp/$package" | cut -d' ' -f1)"
fi
if [ -z "$expected" ] || [ "$expected" != "$actual" ]; then
  echo "install.sh: checksum mismatch for $package" >&2
  exit 1
fi

tar -xzf "$tmp/$package" -C "$tmp" surl
mkdir -p "$dir"
mv "$tmp/surl" "$dir/surl"
chmod 755 "$dir/surl"

echo "Installed $dir/surl"
case ":$PATH:" in
  *":$dir:"*) ;;
  *) echo "To run it by name, add this to your shell profile:"
     echo "  export PATH=\"$dir:\$PATH\"" ;;
esac
