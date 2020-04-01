ARG GOVERSION=1.13
FROM golang:${GOVERSION} AS builder

WORKDIR /go/src/server/
COPY go.mod go.mod
COPY go.sum go.sum
RUN go mod download

COPY api/ api/
COPY pkg/ pkg/
COPY main.go main.go

ENV CGO_ENABLED=0
ENV GO111MODULE=on
ENV GOOS=linux
ENV GOARCH=amd64

RUN go build -a -o /bin/asherah-server main.go

FROM alpine:3.11
WORKDIR /
COPY --from=builder /bin/asherah-server .
ENTRYPOINT ["/asherah-server"]
CMD ["--help"]
