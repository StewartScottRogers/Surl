<#
.SYNOPSIS
    Builds the supplementary linux-x64 Debug (--enable-debug) upstream curl 8.21.0, with
    HTTP/2 and HTTP/3, reproducibly, so its SHA-256 can be pinned (ADR-0173, ADR-0076).

.DESCRIPTION
    The upstream test cases whose <features> list Debug need a curl configured with
    --enable-debug, and no published build is one. So this script builds one from source,
    the same bytes every time, exactly as Build-OpenLdapUpstreamCurl.ps1 does:

      1. downloads, unless already in WorkDirectory, five source tarballs and refuses each
         whose SHA-256 is not the one pinned below: curl-8.21.0.tar.xz (tag curl-8_21_0,
         unpatched), openssl-3.5.9.tar.gz, nghttp2-1.69.0.tar.xz, nghttp3-1.16.0.tar.xz and
         ngtcp2-1.23.0.tar.xz (the library releases stunnel/static-curl 8.21.0 lists);
      2. in a Docker container of the Alpine image pinned below by digest, with the build
         tools pinned below by version, builds OpenSSL, nghttp2, nghttp3 and ngtcp2 as static
         libraries, then curl with --enable-debug statically linked against musl and all of
         them, stripped, with SOURCE_DATE_EPOCH set to curl 8.21.0's release date so no build
         date or build host reaches the binary. In the same tree it also builds
         tests/libtest, which is not exported or pinned (ADR-0173, decision 7);
      3. copies the curl executable to OutputPath and prints its SHA-256.

    The binary's SHA-256 is pinned in UpstreamCurlBuilds.json; CI runs this script on a cache
    miss and then verifies the result against that pin before running it (ADR-0016). Needs
    Docker able to run linux/amd64 containers.

.PARAMETER OutputPath
    Where the built curl is written. Its directory is created if missing.

.PARAMETER WorkDirectory
    Where the source tarballs are kept. Default: a directory named
    debug-upstream-curl-sources in the system temporary directory.

.PARAMETER CompareTwoBuilds
    Builds a second time into a second file beside OutputPath and fails unless both have the
    same SHA-256; the proof a build is reproducible, run when the pin is made or changed.

.EXAMPLE
    .\Build-DebugUpstreamCurl.ps1 -OutputPath C:\UpstreamCurl\debug-curl-8.21.0-linux-x86_64\curl
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$WorkDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'debug-upstream-curl-sources'),
    [switch]$CompareTwoBuilds
)

$ErrorActionPreference = 'Stop'

# Everything that decides the binary's bytes is pinned here.
$image = 'alpine@sha256:5291449c3df73caf6ed85e649dec1b9e818b39a5d8c871e97afc13e9cd5e8fa8'
$packages = 'binutils=2.44-r3 gcc=14.2.0-r6 linux-headers=6.14.2-r0 make=4.4.1-r3 musl=1.2.5-r13 musl-dev=1.2.5-r13 perl=5.40.4-r0 pkgconf=2.4.3-r0'
$sources = @(
    @{ Name = 'curl-8.21.0.tar.xz'; Url = 'https://curl.se/download/curl-8.21.0.tar.xz'; Sha256 = 'AA1B66A70EACE83DC624508745646C08AE561DE512AB403ADFFB93AC87FC72E6' },
    @{ Name = 'openssl-3.5.9.tar.gz'; Url = 'https://github.com/openssl/openssl/releases/download/openssl-3.5.9/openssl-3.5.9.tar.gz'; Sha256 = '603F5602E2EEF00D77FBD429D34DCD5822BB301757A1BC9CDB24C670F1EB859A' },
    @{ Name = 'nghttp2-1.69.0.tar.xz'; Url = 'https://github.com/nghttp2/nghttp2/releases/download/v1.69.0/nghttp2-1.69.0.tar.xz'; Sha256 = '1FB324B6EC2C56F6BDE0658F4139FFD8209FA9E77CE98FD7A5F63AF8D0E508AD' },
    @{ Name = 'nghttp3-1.16.0.tar.xz'; Url = 'https://github.com/ngtcp2/nghttp3/releases/download/v1.16.0/nghttp3-1.16.0.tar.xz'; Sha256 = '776F59A99905C9A348846807B2E5AC9BB3485FC0F8C0250BA803018D5238A16E' },
    @{ Name = 'ngtcp2-1.23.0.tar.xz'; Url = 'https://github.com/ngtcp2/ngtcp2/releases/download/v1.23.0/ngtcp2-1.23.0.tar.xz'; Sha256 = '59D5B4211E96970F2D3D5E6876F73DCE03414800BA04AA56835B132FCE8DE730' }
)
# curl 8.21.0's release date, 2026-06-24T00:00:00Z.
$sourceDateEpoch = 1782259200

New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null
foreach ($source in $sources) {
    $path = Join-Path $WorkDirectory $source.Name
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host "Downloading $($source.Url)"
        Invoke-WebRequest -Uri $source.Url -OutFile $path -UseBasicParsing
    }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actual -ne $source.Sha256) {
        throw "$path hashes to $actual, but $($source.Name) is pinned as $($source.Sha256). Not building from it."
    }
}

# The build runs as one sh script inside the container: configure scripts are sh.
$build = @'
set -eu
for attempt in 1 2 3 4 5; do
  apk add --no-cache -q $PACKAGES && break
  [ "$attempt" = 5 ] && exit 1
  sleep 5
done
mkdir -p /build && cd /build
tar -xzf /sources/openssl-3.5.9.tar.gz
tar -xJf /sources/nghttp2-1.69.0.tar.xz
tar -xJf /sources/nghttp3-1.16.0.tar.xz
tar -xJf /sources/ngtcp2-1.23.0.tar.xz
tar -xJf /sources/curl-8.21.0.tar.xz
export CFLAGS='-O2 -ffile-prefix-map=/build=.'
export PKG_CONFIG_PATH=/build/prefix/lib/pkgconfig
cd /build/openssl-3.5.9
./Configure linux-x86_64 no-shared no-tests no-docs no-module --prefix=/build/prefix --libdir=lib --openssldir=/etc/ssl >/dev/null
make -s -j"$(nproc)" >/dev/null
make -s install_sw >/dev/null
cd /build/nghttp2-1.69.0
./configure -q --prefix=/build/prefix --disable-shared --enable-static --enable-lib-only >/dev/null
make -s -j"$(nproc)" >/dev/null
make -s install >/dev/null
cd /build/nghttp3-1.16.0
./configure -q --prefix=/build/prefix --disable-shared --enable-static --enable-lib-only >/dev/null
make -s -j"$(nproc)" >/dev/null
make -s install >/dev/null
cd /build/ngtcp2-1.23.0
./configure -q --prefix=/build/prefix --disable-shared --enable-static --enable-lib-only \
  --with-openssl >/dev/null
make -s -j"$(nproc)" >/dev/null
make -s install >/dev/null
cd /build/curl-8.21.0
./configure -q --disable-shared --enable-static --enable-debug --disable-docs --disable-manual \
  --with-openssl=/build/prefix --with-nghttp2=/build/prefix --with-nghttp3=/build/prefix \
  --with-ngtcp2=/build/prefix --enable-alt-svc \
  --without-libpsl --without-zlib --without-brotli --without-zstd --without-libidn2 \
  --without-libssh2 --disable-ldap --disable-ldaps --with-ca-bundle=/etc/ssl/certs/ca-certificates.crt \
  LDFLAGS='-static -L/build/prefix/lib' CPPFLAGS=-I/build/prefix/include
# libtool drops a bare -static when it links the program; -all-static is what keeps it.
make -s -j"$(nproc)" LDFLAGS='-static -all-static -L/build/prefix/lib' >/dev/null
# tests/libtest is built in the same tree for a later decision; it is neither exported nor pinned.
make -s -C tests/libtest -j"$(nproc)" LDFLAGS='-static -all-static -L/build/prefix/lib' >/dev/null
strip src/curl
cp src/curl /out/curl
'@ -replace "`r`n", "`n"

function Build-Once([string]$Destination) {
    $outputDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($Destination))
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    $staging = Join-Path $outputDirectory '.debug-upstream-curl-build'
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    try {
        & docker run --rm --platform linux/amd64 --hostname debug-upstream-curl `
            -e "PACKAGES=$packages" -e "SOURCE_DATE_EPOCH=$sourceDateEpoch" -e 'TZ=UTC' -e 'LC_ALL=C' `
            -v "$((Resolve-Path -LiteralPath $WorkDirectory).Path):/sources:ro" -v "${staging}:/out" `
            $image sh -c $build
        if ($LASTEXITCODE -ne 0) { throw "The build container exited $LASTEXITCODE." }
        Move-Item -Force -LiteralPath (Join-Path $staging 'curl') -Destination $Destination
    }
    finally {
        Remove-Item -Recurse -Force -LiteralPath $staging -ErrorAction SilentlyContinue
    }
    return (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
}

$first = Build-Once $OutputPath
if ($CompareTwoBuilds) {
    $secondPath = "$OutputPath.second"
    try {
        $second = Build-Once $secondPath
    }
    finally {
        Remove-Item -Force -LiteralPath $secondPath -ErrorAction SilentlyContinue
    }
    if ($second -ne $first) { throw "Two clean builds differ: $first and $second. The build is not reproducible." }
    Write-Host "Two clean builds agree: $first"
}
Write-Output "$first  $OutputPath"
