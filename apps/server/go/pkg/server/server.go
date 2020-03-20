package server

import (
	"io"

	pb "github.com/godaddy/asherah/apps/server/go/api"
)

var UnitializedSessionResponse = &pb.SessionResponse{
	Response: &pb.SessionResponse_ErrorResponse{
		ErrorResponse: &pb.ErrorResponse{Message: "session not yet initialized"},
	},
}

var SessionAlreadyInitializedResponse = &pb.SessionResponse{
	Response: &pb.SessionResponse_ErrorResponse{
		ErrorResponse: &pb.ErrorResponse{Message: "session has already been initialized"},
	},
}

type requestHandler interface {
	Decrypt(*pb.SessionRequest) *pb.SessionResponse
	Encrypt(*pb.SessionRequest) *pb.SessionResponse
	GetSession(*pb.SessionRequest) *pb.SessionResponse
}

type AppEncryption struct {
	pb.UnimplementedAppEncryptionServer
	streamerFactory
}

type streamerFactory interface {
	NewStreamer() *streamer
}

type streamerFactoryFunc func() *streamer

func (s streamerFactoryFunc) NewStreamer() *streamer {
	return s()
}

func (a *AppEncryption) Session(stream pb.AppEncryption_SessionServer) error {
	s := a.NewStreamer()

	return s.Stream(stream)
}

type streamer struct {
	handlerFactory
	handler requestHandler
}

type handlerFactory interface {
	NewHandler() requestHandler
}

func (s *streamer) NewHandler() requestHandler {
	if s.handlerFactory != nil {
		return s.handlerFactory.NewHandler()
	}
	return &defaultHandler{}
}

func (d *streamer) Stream(stream pb.AppEncryption_SessionServer) error {
	for {
		in, err := stream.Recv()
		if err == io.EOF {
			// TODO: close session
			return nil
		}
		if err != nil {
			return err
		}

		var resp *pb.SessionResponse

		switch in.Request.(type) {
		case *pb.SessionRequest_Decrypt:
			if d.handler == nil {
				resp = UnitializedSessionResponse
				break
			}
			resp = d.handler.Decrypt(in)
		case *pb.SessionRequest_Encrypt:
			if d.handler == nil {
				resp = UnitializedSessionResponse
				break
			}
			resp = d.handler.Encrypt(in)
		case *pb.SessionRequest_GetSession:
			if d.handler != nil {
				resp = SessionAlreadyInitializedResponse
				break
			}

			d.handler = d.NewHandler()
			resp = d.handler.GetSession(in)
		}

		if err := stream.Send(resp); err != nil {
			return err
		}
	}
}

type defaultHandler struct {
}

func (h *defaultHandler) Decrypt(r *pb.SessionRequest) *pb.SessionResponse {
	return nil
}

func (h *defaultHandler) Encrypt(r *pb.SessionRequest) *pb.SessionResponse {
	return nil
}

func (h *defaultHandler) GetSession(r *pb.SessionRequest) *pb.SessionResponse {
	return new(pb.SessionResponse)
}

func NewAppEncryption() *AppEncryption {
	return &AppEncryption{
		streamerFactory: streamerFactoryFunc(func() *streamer {
			return &streamer{}
		}),
	}
}
