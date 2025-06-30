#!/bin/bash
if [ -z "$FORMAT" ]; then
    FORMAT='png size 600,400 enhanced font "Arial,10"'
fi
OUTPUT="out.${FORMAT%% *}"

# This script assumes the memory file format has:
# Column 1: Request count
# Column 2: Heap memory usage (MB)
# Column 3: Stack memory usage (MB)
# Column 4: Total memory usage (MB)

FILE="$1"
NAME="$(basename "$FILE")"
NAME="${NAME%.*}"
NAME="${NAME#*_}"

ARG="set datafile separator ',';\
    set xlabel 'Requests';\
    set xtics rotate by 45 right;\
    set ylabel 'Memory Usage (MB)' offset 1;\
    set yrange [0:];\
    set key top left;\
    set grid;\
    set colors classic;\
    set style line 1 lc rgb '#0060ad' lt 1 lw 2 pt 7 ps 1.5;\
    set style line 2 lc rgb '#dd181f' lt 1 lw 2 pt 5 ps 1.5;\
    set style line 3 lc rgb '#00cc00' lt 1 lw 2 pt 9 ps 1.5;\
    set title 'Memory Usage for $NAME';\
    set terminal $FORMAT;\
    set output '$OUTPUT';\
    plot '$FILE' every ::1 using 1:2 with lines title 'Heap', \
         '$FILE' every ::1 using 1:3 with lines title 'Stack', \
         '$FILE' every ::1 using 1:4 with lines title 'Total'"

gnuplot -e "$ARG"