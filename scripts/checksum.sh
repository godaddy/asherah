#!/bin/bash
# Based of https://github.com/chrisbanes/tivi/blob/master/checksum.sh
RESULT_FILE=$1
PROJECT_FILE=$2

if [[ -z "$2" ]]
  then
    echo "No path supplied to check for project files"
    exit 1
fi

if [[ -f ${RESULT_FILE} ]]; then
  rm ${RESULT_FILE}
fi
touch ${RESULT_FILE}

checksum_file() {
  echo `openssl md5 $1 | awk '{print $2}'`
}

FILES=()
while read -r -d ''; do
	FILES+=("$REPLY")
done < <(find . -name ${PROJECT_FILE} -type f -print0)

# Loop through files and append MD5 to result file
for FILE in ${FILES[@]}; do
	echo `checksum_file ${FILE}` >> ${RESULT_FILE}
done
# Now sort the file so that it is
sort ${RESULT_FILE} -o ${RESULT_FILE}
