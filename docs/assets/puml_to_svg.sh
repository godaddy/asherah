#!/bin/sh

# Based on script from https://blog.anoff.io/2018-07-31-diagrams-with-plantuml/
# converts all puml files to svg

BASEDIR=$(dirname "$0")
for FILE in $BASEDIR/*.puml; do
  echo Converting $FILE..
  FILE_SVG=${FILE//puml/svg}
  FILE_PDF=${FILE//puml/pdf}
  cat $FILE | docker run --rm -i think/plantuml > $FILE_SVG
done
mv $BASEDIR/*.svg $BASEDIR/../images
echo Done