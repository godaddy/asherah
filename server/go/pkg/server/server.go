package server

import (
	"context"
	"io"
	"log"

	awssession "github.com/aws/aws-sdk-go/aws/session"
	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
	"github.com/godaddy/asherah/go/securememory/memguard"

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
	options Options
}

type handlerFactory interface {
	NewHandler() requestHandler
}

func (s *streamer) NewHandler() requestHandler {
	if s.handlerFactory != nil {
		return s.handlerFactory.NewHandler()
	}

	crypto := aead.NewAES256GCM()

	sf := appencryption.NewSessionFactory(
		&appencryption.Config{
			Service: s.options.ServiceName,
			Product: s.options.ProductId,
			Policy: appencryption.NewCryptoPolicy(
				appencryption.WithExpireAfterDuration(s.options.ExpireAfter),
				appencryption.WithRevokeCheckInterval(s.options.CheckInterval),
			),
		},
		NewMetastore(s.options),
		NewKMS(s.options, crypto),
		crypto,
		appencryption.WithSecretFactory(new(memguard.SecretFactory)),
		appencryption.WithMetrics(false),
	)

	return &defaultHandler{
		sessionFactory: sf,
	}
}

func NewMetastore(opts Options) appencryption.Metastore {
	if opts.Metastore == "rdbms" {
		// TODO: support other databases
		db, err := newMysql(opts.ConnectionString)
		if err != nil {
			panic(err)
		}

		return persistence.NewSQLMetastore(db)
	}

	sess := awssession.Must(awssession.NewSessionWithOptions(awssession.Options{
		SharedConfigState: awssession.SharedConfigEnable,
	}))
	return persistence.NewDynamoDBMetastore(sess)
}

func NewKMS(opts Options, crypto appencryption.AEAD) appencryption.KeyManagementService {
	if opts.KMS == "static" {
		kms, err := kms.NewStatic("thisistotallynotsecretdonotuse!!", aead.NewAES256GCM())
		if err != nil {
			panic(err)
		}
		return kms
	}

	kms, err := kms.NewAWS(crypto, opts.PreferredRegion, opts.RegionMap)
	if err != nil {
		panic(err)
	}
	return kms
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
	GetSession(id string) (*appencryption.Session, error)
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
	log.Println("handling decrypt")
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
	log.Println("handling encrypt")

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
	partition := r.GetGetSession().GetPartitionId()

	log.Println("handling get-session for", partition)

	s, err := h.sessionFactory.GetSession(partition)
	if err != nil {
		return newErrorResponse(err.Error())
	}

	h.session = s
	return new(pb.SessionResponse)
}

func (h *defaultHandler) Close() error {
	log.Println("closing session")
	return h.session.Close()
}

func NewAppEncryption(options Options) *AppEncryption {
	return &AppEncryption{
		streamerFactory: streamerFactoryFunc(func() *streamer {
			return &streamer{options: options}
		}),
	}
}
