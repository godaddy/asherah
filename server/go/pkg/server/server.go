package server

import (
	"context"
	"io"

	"github.com/godaddy/asherah/go/appencryption"

	pb "github.com/godaddy/asherah/server/go/api"
)

var (
	UnitializedSessionResponse        = newErrorResponse("session not yet initialized")
	SessionAlreadyInitializedResponse = newErrorResponse("session has already been initialized")
)

func newErrorResponse(message string) *pb.SessionResponse {
	return &pb.SessionResponse{
		Response: &pb.SessionResponse_ErrorResponse{
			ErrorResponse: &pb.ErrorResponse{Message: message},
		},
	}
}

type requestHandler interface {
	Decrypt(context.Context, *pb.SessionRequest) *pb.SessionResponse
	Encrypt(context.Context, *pb.SessionRequest) *pb.SessionResponse
	GetSession(*pb.SessionRequest) *pb.SessionResponse
	Close() error
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
	defer func() {
		if d.handler != nil {
			d.handler.Close()
		}
	}()

	for {
		in, err := stream.Recv()
		if err == io.EOF {
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
			resp = d.handler.Decrypt(stream.Context(), in)
		case *pb.SessionRequest_Encrypt:
			if d.handler == nil {
				resp = UnitializedSessionResponse
				break
			}
			resp = d.handler.Encrypt(stream.Context(), in)
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

type sessionFactory interface {
	GetSession(id string) *appencryption.Session
}

type session interface {
	EncryptContext(ctx context.Context, data []byte) (*appencryption.DataRowRecord, error)
	DecryptContext(ctx context.Context, d appencryption.DataRowRecord) ([]byte, error)
	Close() error
}

type defaultHandler struct {
	sessionFactory sessionFactory
	session        session
}

func (h *defaultHandler) Decrypt(ctx context.Context, r *pb.SessionRequest) *pb.SessionResponse {
	drr := fromProtobufDRR(r.GetDecrypt().GetDataRowRecord())
	data, err := h.session.DecryptContext(ctx, *drr)
	if err != nil {
		return newErrorResponse(err.Error())
	}

	return &pb.SessionResponse{
		Response: &pb.SessionResponse_DecryptResponse{
			DecryptResponse: &pb.DecryptResponse{
				Data: data,
			},
		},
	}
}

func fromProtobufDRR(drr *pb.DataRowRecord) *appencryption.DataRowRecord {
	return &appencryption.DataRowRecord{
		Data: drr.Data,
		Key: &appencryption.EnvelopeKeyRecord{
			EncryptedKey: drr.Key.Key,
			Created:      drr.Key.Created,
			ParentKeyMeta: &appencryption.KeyMeta{
				ID:      drr.Key.ParentKeyMeta.KeyId,
				Created: drr.Key.ParentKeyMeta.Created,
			},
		},
	}
}

func (h *defaultHandler) Encrypt(ctx context.Context, r *pb.SessionRequest) *pb.SessionResponse {
	drr, err := h.session.EncryptContext(ctx, r.GetEncrypt().GetData())
	if err != nil {
		return newErrorResponse(err.Error())
	}

	return &pb.SessionResponse{
		Response: &pb.SessionResponse_EncryptResponse{
			EncryptResponse: &pb.EncryptResponse{
				DataRowRecord: toProtobufDRR(drr),
			},
		},
	}
}

func toProtobufDRR(drr *appencryption.DataRowRecord) *pb.DataRowRecord {
	return &pb.DataRowRecord{
		Data: drr.Data,
		Key: &pb.EnvelopeKeyRecord{
			Created: drr.Key.Created,
			Key:     drr.Key.EncryptedKey,
			ParentKeyMeta: &pb.KeyMeta{
				Created: drr.Key.ParentKeyMeta.Created,
				KeyId:   drr.Key.ParentKeyMeta.ID,
			},
		},
	}
}

func (h *defaultHandler) GetSession(r *pb.SessionRequest) *pb.SessionResponse {
	h.session = h.sessionFactory.GetSession(r.GetGetSession().GetPartitionId())
	return new(pb.SessionResponse)
}

func (h *defaultHandler) Close() error {
	return h.session.Close()
}

func NewAppEncryption() *AppEncryption {
	return &AppEncryption{
		streamerFactory: streamerFactoryFunc(func() *streamer {
			return &streamer{}
		}),
	}
}
