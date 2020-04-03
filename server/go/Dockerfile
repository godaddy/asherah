ARG GOVERSION=1.13
ARG ALPINEVERSION=3.11
FROM golang:${GOVERSION}-alpine${ALPINEVERSION} AS builder

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

FROM alpine:${ALPINEVERSION}
WORKDIR /
COPY --from=builder /bin/asherah-server .
ENTRYPOINT ["/asherah-server"]
CMD ["--help"]
