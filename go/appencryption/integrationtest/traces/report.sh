#!/bin/bash
set -e

report() {
	NAME="$1"
    TESTARGS="-p 1 -timeout=3h -run=$NAME"
	go test -v $TESTARGS | tee "out/$NAME.txt"

	NAME=$(echo "$NAME" | tr '[:upper:]' '[:lower:]')
	./visualize-request.sh out/request_$NAME-*.txt
	for OUTPUT in out.*; do
		mv -v "$OUTPUT" "out/$NAME-requests.${OUTPUT#*.}"
	done
	./visualize-size.sh out/size_$NAME-*.txt
	for OUTPUT in out.*; do
		mv -v "$OUTPUT" "out/$NAME-cachesize.${OUTPUT#*.}"
	done
}

# use first arg or default to a small subset
TRACES="$@"
if [ -z "$TRACES" ]; then
    TRACES="Financial OLTP ORMBusy Zipf"
fi

# TRACES="Multi2 ORMBusy ORMNight Glimpse OLTP Sprite Financial WebSearch Wikipedia YouTube Zipf"
for TRACE in $TRACES; do
	report $TRACE
done
