# Test Ubuntu mirrors

## Description
Test Ubuntu mirrors is a cross-platform tool which checks mirrors (by default from the official http://mirrors.ubuntu.com/ or from a supplied text file)
for the availability of packages for a specific distribution and architecture.
Optionally you can test the speed of these mirrors with the supplied helper shell scripts and update your apt sources list.
It's very fast and flexible and has lots of options.

## Syntax

```console
TestUbuntuMirrors 1.0
Copyright (C) 2023 TestUbuntuMirrors

  --distribution                              Required. Linux distribution for example: lunar, jammy, impish, hirsute, groovy, focal, bionic

  --architecture                              Required. Architecture for example: arm64, armhf, ppc64el, riscv64, s390x

  --timeout                                   (Default: 2) Timeout in seconds

  --maxmirrorstocheck                         (Default: 2147483647) Max mirrors to check

  --alwaysredownloadmirrorsforcountries       (Default: false) Always redownload mirrors for countries, even if available from cache

  --ignorepreviousmirrorcheckresults          (Default: false) Ignore previous mirror check results when checking if packages for given distribution and architecture are available

  --onlyretryfailedmirrorswithlowertimeout    (Default: false) Only retry failed mirrors which had a lower timeout in previous run than given timeout. Only works when switch --RetryFailedMirrorsWithTimeOut is set

  --retryfailedmirrorswithtimeout             (Default: false) Retry failed mirrors which had a timeout error

  --donotexportworkingmirrorstotextfile       (Default: false) Do not export working mirrors to plain text file, for example for use as input in netselect

  --usefileasinputformirrors                  Use specified file as input for mirrors to be checked

  --urlmirrors                                (Default: http://mirrors.ubuntu.com/) Url of website containing mirrors for each country. Website must contain .txt file(s) for each country using official 2 letter country codes.

  --countrymirrorfilters                      Filter mirror country files for example:
                                               --CountryMirrorFilters NL EN

  --help                                      Display this help screen.

  --version                                   Display version information.
```

When you program runs you can:  
-pause by pressing [SPACE]  
-quit by pressing [ESCAPE]

## How to use 

1. TestUbuntuMirrors can be run on any machine and can search for any Linux Ubuntu architecture and distribution.
For example to search for `Jammy` (Ubuntu 22.x) packages for `arm64` processors:
```console
./TestUbuntuMirrors --distribution jammy --architecture arm64
```

2. After searching for working mirrors that have the packages available, you can optionally test the speed of them:
```console
./test-mirrors-with-netselect.sh jammy arm64
```

You can also supply your own list of working mirrors by specifying a text file where each line contains one url:
```console
./test-mirrors-with-netselect.sh mymirrors.txt
```

3. To update your `/etc/apt/sources.list` with a new fast mirror which has packages avaialble for your distribution and architecture use:
```console
./replace-mirrors-in-apt-sources.sh https://mirror.kumi.systems/ubuntu-ports/
```


If you want to find out which Linux Ubuntu package distribution you need you can use:
```console
cat /etc/os-release | grep VERSION_CODENAME
```

To check which Linux Ubuntu package architecture use:
```console
uname -m -p -i
```

`armv71` means you need `armhf` (for example Raspberry Pi 3)  
`aarch64` means you need `arm64` (for example Raspberry Pi 4, Odroid-M1, Rock Pi 5)  

