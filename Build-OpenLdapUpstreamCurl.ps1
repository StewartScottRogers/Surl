<#
.SYNOPSIS
    Builds the supplementary linux-x64 upstream curl 8.21.0 whose ldap and ldaps run over
    OpenLDAP (lib/openldap.c), reproducibly, so its SHA-256 can be pinned (ADR-0076).

.DESCRIPTION
    No published build of upstream curl 8.21.0 both leaves curl unpatched and runs its LDAP
    over OpenLDAP (the stunnel/static-curl builds carry no LDAP; distribution packages carry
    patches). So this script builds one from source, the same bytes every time:

      1. downloads, unless already in WorkDirectory, three source tarballs and refuses each
         whose SHA-256 is not the one pinned below: curl-8.21.0.tar.xz (tag curl-8_21_0,
         unpatched), openldap-2.6.15.tgz and openssl-3.5.9.tar.gz;
      2. in a Docker container of the Alpine image pinned below by digest, with the build
         tools pinned below by version (apk refuses a version its repository no longer
         has, which fails the build rather than changing it), builds OpenSSL and OpenLDAP's
         libldap and liblber as static libraries, then curl statically linked against
         musl and both of them, stripped, with SOURCE_DATE_EPOCH set to curl 8.21.0's
         release date so no build date or build host reaches the binary;
      3. copies the curl executable to OutputPath and prints its SHA-256.

    The binary's SHA-256 is pinned in UpstreamCurlBuilds.json; CI runs this script on a
    cache miss and then verifies the result against that pin before running it, exactly as
    it verifies a downloaded build (ADR-0016). Needs Docker able to run linux/amd64
    containers.

.PARAMETER OutputPath
    Where the built curl is written. Its directory is created if missing.

.PARAMETER WorkDirectory
    Where the source tarballs are kept. Default: a directory named
    openldap-upstream-curl-sources in the system temporary directory.

.EXAMPLE
    .\Build-OpenLdapUpstreamCurl.ps1 -OutputPath C:\UpstreamCurl\openldap-curl-8.21.0-linux-x86_64\curl
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$WorkDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'openldap-upstream-curl-sources')
)

$ErrorActionPreference = 'Stop'

# Everything that decides the binary's bytes is pinned here.
$image = 'alpine@sha256:5291449c3df73caf6ed85e649dec1b9e818b39a5d8c871e97afc13e9cd5e8fa8'
$packages = 'binutils=2.44-r3 gcc=14.2.0-r6 linux-headers=6.14.2-r0 make=4.4.1-r3 musl-dev=1.2.5-r12 perl=5.40.4-r0'
$sources = @(
    @{ Name = 'curl-8.21.0.tar.xz'; Url = 'https://curl.se/download/curl-8.21.0.tar.xz'; Sha256 = 'AA1B66A70EACE83DC624508745646C08AE561DE512AB403ADFFB93AC87FC72E6' },
    @{ Name = 'openldap-2.6.15.tgz'; Url = 'https://www.openldap.org/software/download/OpenLDAP/openldap-release/openldap-2.6.15.tgz'; Sha256 = 'BC91225DBFC50354033B1303BC91D1A7F6DDD1DC32FAC950D79C28FE66D6BCA8' },
    @{ Name = 'openssl-3.5.9.tar.gz'; Url = 'https://github.com/openssl/openssl/releases/download/openssl-3.5.9/openssl-3.5.9.tar.gz'; Sha256 = '603F5602E2EEF00D77FBD429D34DCD5822BB301757A1BC9CDB24C670F1EB859A' }
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
tar -xzf /sources/openldap-2.6.15.tgz
tar -xJf /sources/curl-8.21.0.tar.xz
export CFLAGS='-O2 -ffile-prefix-map=/build=.'
cd /build/openssl-3.5.9
./Configure linux-x86_64 no-shared no-tests no-docs no-module --prefix=/build/prefix --libdir=lib --openssldir=/etc/ssl >/dev/null
make -s -j"$(nproc)" >/dev/null
make -s install_sw >/dev/null
cd /build/openldap-2.6.15
./configure -q --prefix=/build/prefix --disable-shared --enable-static --disable-slapd \
  --with-tls=openssl --without-cyrus-sasl --without-systemd --without-fetch --without-argon2 \
  CPPFLAGS=-I/build/prefix/include LDFLAGS=-L/build/prefix/lib
make -s depend >/dev/null
make -s -C include >/dev/null
make -s -C libraries >/dev/null
make -s -C include install >/dev/null
make -s -C libraries install >/dev/null
cd /build/curl-8.21.0
./configure -q --disable-shared --enable-static --disable-docs --disable-manual \
  --with-openssl=/build/prefix --with-ldap=/build/prefix --enable-ldap --enable-ldaps --enable-ntlm \
  --without-libpsl --without-zlib --without-brotli --without-zstd --without-libidn2 \
  --without-nghttp2 --without-libssh2 --with-ca-bundle=/etc/ssl/certs/ca-certificates.crt \
  LDFLAGS='-static -L/build/prefix/lib' CPPFLAGS=-I/build/prefix/include LIBS='-lssl -lcrypto'
# libtool drops a bare -static when it links the program; -all-static is what keeps it.
make -s -j"$(nproc)" LDFLAGS='-static -all-static -L/build/prefix/lib' >/dev/null
strip src/curl
cp src/curl /out/curl
'@ -replace "`r`n", "`n"

$outputDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$staging = Join-Path $outputDirectory '.openldap-upstream-curl-build'
New-Item -ItemType Directory -Force -Path $staging | Out-Null
try {
    & docker run --rm --platform linux/amd64 --hostname openldap-upstream-curl `
        -e "PACKAGES=$packages" -e "SOURCE_DATE_EPOCH=$sourceDateEpoch" -e 'TZ=UTC' -e 'LC_ALL=C' `
        -v "$((Resolve-Path -LiteralPath $WorkDirectory).Path):/sources:ro" -v "${staging}:/out" `
        $image sh -c $build
    if ($LASTEXITCODE -ne 0) { throw "The build container exited $LASTEXITCODE." }
    Move-Item -Force -LiteralPath (Join-Path $staging 'curl') -Destination $OutputPath
}
finally {
    Remove-Item -Recurse -Force -LiteralPath $staging -ErrorAction SilentlyContinue
}
Write-Output "$((Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash)  $OutputPath"
