#!/bin/bash
if [ -z "$FORMAT" ]; then
    FORMAT='png size 600,400 enhanced font "Arial,10"'
fi
OUTPUT="out.${FORMAT%% *}"
PLOTARG=""

for f in "$@"; do
    if [ ! -z "$PLOTARG" ]; then
        PLOTARG="$PLOTARG,"
    fi
    NAME="$(basename "$f")"
    NAME="${NAME%.*}"
    NAME="${NAME#*_}"
    
    # Plot KMS operations (column 2) vs Metastore operations (column 5) against request count (column 1)
    PLOTARG="$PLOTARG '$f' every ::1 using 1:2 with lines title 'KMS Ops ($NAME)', \
             '$f' every ::1 using 1:5 with lines title 'Metastore Ops ($NAME)'"
done

ARG="set datafile separator ',';\
    set xlabel 'Requests';\
    set xtics rotate by 45 right;\
    set ylabel 'Operation Count' offset 1;\
    set yrange [0:];\
    set key top left;\
    set grid;\
    set colors classic;\
    set style line 1 lc rgb '#0060ad' lt 1 lw 2 pt 7 ps 1.5;\
    set style line 2 lc rgb '#dd181f' lt 1 lw 2 pt 5 ps 1.5;\
    set title 'KMS vs Metastore Operations';\
    set terminal $FORMAT;\
    set output '$OUTPUT';\
    plot $PLOTARG"

gnuplot -e "$ARG"