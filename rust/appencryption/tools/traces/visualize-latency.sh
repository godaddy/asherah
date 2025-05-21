#!/bin/bash
if [ -z "$FORMAT" ]; then
    FORMAT='png size 600,400 enhanced font "Arial,10"'
fi
OUTPUT="out.${FORMAT%% *}"
PLOTARG=""

# This script assumes the latency file format has:
# Column 1: Request count
# Column 2: Min latency (ms)
# Column 3: Max latency (ms)
# Column 4: Avg latency (ms)
# Column 5: P50 latency (ms)
# Column 6: P95 latency (ms)
# Column 7: P99 latency (ms)

for f in "$@"; do
    if [ ! -z "$PLOTARG" ]; then
        PLOTARG="$PLOTARG,"
    fi
    NAME="$(basename "$f")"
    NAME="${NAME%.*}"
    NAME="${NAME#*_}"
    
    # Plot latency percentiles against request count
    PLOTARG="$PLOTARG '$f' every ::1 using 1:4 with lines title 'Avg ($NAME)', \
             '$f' every ::1 using 1:5 with lines title 'P50 ($NAME)', \
             '$f' every ::1 using 1:6 with lines title 'P95 ($NAME)', \
             '$f' every ::1 using 1:7 with lines title 'P99 ($NAME)'"
done

ARG="set datafile separator ',';\
    set xlabel 'Requests';\
    set xtics rotate by 45 right;\
    set ylabel 'Latency (ms)' offset 1;\
    set yrange [0:];\
    set key top left;\
    set grid;\
    set colors classic;\
    set style line 1 lc rgb '#0060ad' lt 1 lw 2 pt 7 ps 1.5;\
    set style line 2 lc rgb '#dd181f' lt 1 lw 2 pt 5 ps 1.5;\
    set style line 3 lc rgb '#00cc00' lt 1 lw 2 pt 9 ps 1.5;\
    set style line 4 lc rgb '#cc6600' lt 1 lw 2 pt 11 ps 1.5;\
    set title 'Operation Latency Distribution';\
    set terminal $FORMAT;\
    set output '$OUTPUT';\
    plot $PLOTARG"

gnuplot -e "$ARG"