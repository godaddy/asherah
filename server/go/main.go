//go:generate protoc -I ../protos --go_out=plugins=grpc:. ../protos/appencryption.proto

// Package main implements a gRPC server that ...
package main

import (
	"errors"
	"fmt"
	"log"
	"net"
	"os"
	"os/signal"
	"syscall"

	"github.com/jessevdk/go-flags"
	"google.golang.org/grpc"

	pb "github.com/godaddy/asherah/server/go/api"
	"github.com/godaddy/asherah/server/go/pkg/server"
)

type Options struct {
	SocketFile flags.Filename `short:"s" long:"socket-file" default:"/tmp/appencryption.sock" description:"The unix domain socket the server will listen on"`
	Asherah    server.Options `group:"Asherah Options"`
}

func main() {
	var opts Options
	parser := flags.NewParser(&opts, flags.Default)

	if _, err := parser.Parse(); err != nil {
		if e, ok := err.(*flags.Error); ok && e.Type == flags.ErrHelp {
			return
		}

		parser.WriteHelp(os.Stdout)
		return
	}

	if err := validateOptions(opts); err != nil {
		fmt.Println("Missing or invalid options:", err.Error())
		fmt.Println()

		parser.WriteHelp(os.Stdout)
		return
	}

	l, err := net.Listen("unix", string(opts.SocketFile))
	if err != nil {
		panic(err)
	}
	defer l.Close()

	server := server.NewAppEncryption(opts.Asherah)
	grpcServer := grpc.NewServer()
	pb.RegisterAppEncryptionServer(grpcServer, server)

	sigs := make(chan os.Signal, 1)
	signal.Notify(sigs, syscall.SIGINT, syscall.SIGTERM)

	go func() {
		sig := <-sigs
		log.Printf("%v received", sig)
		grpcServer.GracefulStop()
		log.Println("graceful shutdown complete")
	}()

	log.Println("starting server")
	grpcServer.Serve(l)
	log.Println("exiting")
}

func validateOptions(opts Options) error {
	if opts.Asherah.Metastore == "rdbms" && len(opts.Asherah.ConnectionString) == 0 {
		return errors.New("--conn is required when --metastore=rdbms")
	}

	if opts.Asherah.KMS == "aws" {
		if len(opts.Asherah.PreferredRegion) == 0 {
			return errors.New("--preferred-region is required when --kms=aws")
		}
		if len(opts.Asherah.PreferredRegion) == 0 {
			return errors.New("--region-map is required when --kms=aws")
		}
	}

	return nil
}
