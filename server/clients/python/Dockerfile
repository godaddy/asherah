FROM python:3.7-alpine as base

FROM base as builder

RUN apk add --update --no-cache \
    build-base \
    linux-headers

RUN mkdir /install
WORKDIR /install

COPY requirements.txt /requirements.txt

RUN pip install --upgrade pip
RUN pip install --prefix /install -r /requirements.txt

FROM base

RUN apk add --no-cache libstdc++

COPY --from=builder /install /usr/local
COPY *.py /app/

WORKDIR /app

ENTRYPOINT ["python", "appencryption_client.py"]
CMD ["--help"]
