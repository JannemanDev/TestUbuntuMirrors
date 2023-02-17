if [[ ($# -ne 1 && $# -ne 2) ]]; then
	echo "Syntax:"
	echo " test-mirrors-with-netselect.sh [distribution] [architecture]"
	echo "   mirrors textfile should be located at: ./checkedMirrors/workingMirrors-[distribution]-[architecture].txt"
	echo " or"
	echo "  test-mirrors-with-netselect.sh [mirrors-textfile]"
	echo "   where each line contains one mirror url"
	exit 1
fi

if [[ ($# -eq 1) ]]; then
	file=$1
fi

if [[ ($# -eq 2) ]]; then
	file="checked-mirrors/workingMirrors-$1-$2.txt"
fi

echo $file

# for more information on netselect see https://github.com/apenwarr/netselect
sudo netselect -vvv -s 10 -t 10 $(cat $file)
