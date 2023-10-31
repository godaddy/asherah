#!/bin/bash
if [ -z "$FORMAT" ]; then
	#FORMAT='svg size 400,300 font "Helvetica,10"'
	# FORMAT='png size 220,180 small noenhanced'
    FORMAT='png size 400,300 small noenhanced'
fi
OUTPUT="out.${FORMAT%% *}"
PLOTARG=""

for f in "$@"; do
	if [ ! -z "$PLOTARG" ]; then
		PLOTARG="$PLOTARG,"
	fi
	NAME="$(basename "$f")" # remove path
	NAME="${NAME%.*}" # remove extension
	NAME="${NAME#*_}" # remove prefix
	PLOTARG="$PLOTARG '$f' every ::1 using 10:9:xtic(10) with lines title '$NAME'" # 10:9 is cache size:op rate
done

ARG="set datafile separator ',';\
	set xlabel 'Cache Size';\
	set xtics rotate by 45 right;\
	set ylabel 'Op Rate' offset 1;\
	set yrange [0:];\
	set key bottom right;\
	set colors classic;\
	set terminal $FORMAT;\
	set output '$OUTPUT';\
	plot $PLOTARG"

gnuplot -e "$ARG"
