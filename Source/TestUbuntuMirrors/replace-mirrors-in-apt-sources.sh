#!/bin/bash

if [ "$1" = "" ] || [ "$2" = "" ]; then
        echo "Syntax:"
        echo " replace-mirror-in-apt-sources.sh [searchMirror] [replaceMirror]"
        exit 1
fi

searchMirror=$(printf '%s\n' "$1" | sed -e 's/[\/&]/\\&/g')
replaceMirror=$(printf '%s\n' "$2" | sed -e 's/[\/&]/\\&/g')
regEx="s/$searchMirror/$replaceMirror/"
sudo sed -i "$regEx" /etc/apt/sources.list
