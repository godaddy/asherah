#!/bin/sh
set -e
# NAMES="financial zipf"
NAMES="financial oltp ormbusy ormnight multi2 youtube websearch zipf"
FORMAT="png"
FILES=""
for N in $NAMES; do
	FILES="$FILES out/$N-requests.$FORMAT out/$N-cachesize.$FORMAT"
done
gm montage -mode concatenate -tile 4x $FILES "out/report-session-cache.$FORMAT"
