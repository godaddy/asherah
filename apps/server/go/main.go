//go:generate protoc -I ../protos --go_out=plugins=grpc:. ../protos/appencryption.proto

// Package main implements a gRPC server that ...
package main

import (
	_ "github.com/godaddy/asherah/apps/server/go/api"
)

func main() {
	// noop
}
