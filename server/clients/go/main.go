package main

import (
	"bytes"
	"context"
	"log"
	"os"
	"time"

	"github.com/jessevdk/go-flags"
	"google.golang.org/grpc"

	pb "github.com/godaddy/asherah/server/go/api"
)

type Options struct {
	SocketFile flags.Filename `short:"s" long:"socket-file" default:"/tmp/appencryption.sock" description:"The unix domain socket the server is listening on"`
}

func main() {
	var opts Options
	parser := flags.NewParser(&opts, flags.Default)

	_, err := parser.Parse()
	if err != nil {
		if e, ok := err.(*flags.Error); ok && e.Type == flags.ErrHelp {
			return
		}

		parser.WriteHelp(os.Stdout)
		return
	}

	log.Println("starting test")
	addr := "unix://" + string(opts.SocketFile)

	log.Printf("dialing %s", addr)
	conn, err := grpc.Dial(addr, grpc.WithBlock(), grpc.WithInsecure())
	if err != nil {
		log.Fatal(err)
	}
	defer conn.Close()

	client := pb.NewAppEncryptionClient(conn)
	runClientTest(client)
}

func runClientTest(client pb.AppEncryptionClient) {
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	log.Println("initiating stream")
	stream, err := client.Session(ctx)
	if err != nil {
		panic(err)
	}

	partition := "partitionid-1"

	log.Println("get session for", partition)
	beginSession(stream, partition)

	secret := []byte(`my "secret" data`)

	log.Println("encrypting:", string(secret))
	drr := encrypt(stream, secret)
	log.Println("received DRR")

	log.Println("decrypting DRR")
	data := decrypt(stream, drr)
	log.Println("received decrypted data:", string(data))

	if !bytes.Equal(data, secret) {
		log.Println("Oh no... something went terribly wrong!")
	}

	log.Println("closing stream")
	if err := stream.CloseSend(); err != nil {
		panic(err)
	}

	log.Println("test completed successfully")
}

func beginSession(stream pb.AppEncryption_SessionClient, id string) {
	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_GetSession{
			GetSession: &pb.GetSession{PartitionId: id},
		},
	}

	sendAndRecv(stream, req)
}

func encrypt(stream pb.AppEncryption_SessionClient, data []byte) *pb.DataRowRecord {
	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_Encrypt{
			Encrypt: &pb.Encrypt{
				Data: data,
			},
		},
	}

	resp := sendAndRecv(stream, req)
	drr := resp.GetEncryptResponse().GetDataRowRecord()

	return drr
}

func decrypt(stream pb.AppEncryption_SessionClient, drr *pb.DataRowRecord) []byte {
	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_Decrypt{
			Decrypt: &pb.Decrypt{
				DataRowRecord: drr,
			},
		},
	}

	resp := sendAndRecv(stream, req)
	data := resp.GetDecryptResponse().GetData()
	return data
}

func sendAndRecv(stream pb.AppEncryption_SessionClient, req *pb.SessionRequest) *pb.SessionResponse {
	if err := stream.Send(req); err != nil {
		panic(err)
	}

	log.Println("receiving...")
	in, err := stream.Recv()
	if err != nil {
		panic(err)
	}

	if resp := in.GetErrorResponse(); resp != nil {
		panic(resp.GetMessage())
	}

	return in
}
