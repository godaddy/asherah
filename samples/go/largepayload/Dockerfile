ARG GOVERSION=1.25
FROM golang:${GOVERSION}-bookworm AS builder

WORKDIR /src

# Copy Go modules needed for replace directives
COPY go/appencryption/ go/appencryption/
COPY go/securememory/ go/securememory/
COPY samples/go/largepayload/ samples/go/largepayload/

WORKDIR /src/samples/go/largepayload

RUN go mod download
RUN CGO_ENABLED=0 GOOS=linux go build -o /bin/largepayload .

FROM debian:bookworm-slim

COPY --from=builder /bin/largepayload /usr/local/bin/largepayload

ENTRYPOINT ["largepayload"]
CMD ["-sizes", "25,50,100"]
