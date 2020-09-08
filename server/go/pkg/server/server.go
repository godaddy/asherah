package server

import (
	"context"
	"io"
	"log"

	"github.com/aws/aws-sdk-go/aws"
	awssession "github.com/aws/aws-sdk-go/aws/session"
	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
	"github.com/godaddy/asherah/go/securememory/memguard"

	pb "github.com/godaddy/asherah/server/go/api"
)

var (
	UninitializedSessionResponse      = newErrorResponse("session not yet initialized")
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
	handler        requestHandler
	sessionFactory sessionFactory
}

type handlerFactory interface {
	NewHandler() requestHandler
}

func (s *streamer) NewHandler() requestHandler {
	if s.handlerFactory != nil {
		return s.handlerFactory.NewHandler()
	}

	return &defaultHandler{
		sessionFactory: s.sessionFactory,
	}
}

func NewMetastore(opts *Options) appencryption.Metastore {
	switch opts.Metastore {
	case "rdbms":
		// TODO: support other databases
		db, err := newMysql(opts.ConnectionString)
		if err != nil {
			panic(err)
		}

		return persistence.NewSQLMetastore(db)
	case "dynamodb":
		awsOpts := awssession.Options{
			SharedConfigState: awssession.SharedConfigEnable,
		}

		if len(opts.DynamoDBEndpoint) > 0 {
			awsOpts.Config.Endpoint = aws.String(opts.DynamoDBEndpoint)
		}

		if len(opts.DynamoDBRegion) > 0 {
			awsOpts.Config.Region = aws.String(opts.DynamoDBRegion)
		}

		return persistence.NewDynamoDBMetastore(
			awssession.Must(awssession.NewSessionWithOptions(awsOpts)),
			persistence.WithDynamoDBRegionSuffix(opts.EnableRegionSuffix),
			persistence.WithTableName(opts.DynamoDBTableName),
		)
	default:
		return persistence.NewMemoryMetastore()
	}
}

func NewKMS(opts *Options, crypto appencryption.AEAD) appencryption.KeyManagementService {
	if opts.KMS == "static" {
		m, err := kms.NewStatic("thisIsAStaticMasterKeyForTesting", aead.NewAES256GCM())
		if err != nil {
			panic(err)
		}

		return m
	}

	m, err := kms.NewAWS(crypto, opts.PreferredRegion, opts.RegionMap)
	if err != nil {
		panic(err)
	}

	return m
}

func (s *streamer) Stream(stream pb.AppEncryption_SessionServer) error {
	defer func() {
		if s.handler != nil {
			s.handler.Close()
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

		resp := s.handleRequest(stream.Context(), in)
		if err := stream.Send(resp); err != nil {
			log.Println("unexpected error on send:", err.Error())
			return err
		}
	}
}

func (s *streamer) handleRequest(ctx context.Context, in *pb.SessionRequest) *pb.SessionResponse {
	switch in.Request.(type) {
	case *pb.SessionRequest_Decrypt:
		if s.handler == nil {
			return UninitializedSessionResponse
		}

		return s.handler.Decrypt(ctx, in)
	case *pb.SessionRequest_Encrypt:
		if s.handler == nil {
			return UninitializedSessionResponse
		}

		return s.handler.Encrypt(ctx, in)
	case *pb.SessionRequest_GetSession:
		if s.handler != nil {
			return SessionAlreadyInitializedResponse
		}

		s.handler = s.NewHandler()

		return s.handler.GetSession(in)
	}

	// TODO: handle default
	return nil
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
	partition      string
}

func (h *defaultHandler) Decrypt(ctx context.Context, r *pb.SessionRequest) *pb.SessionResponse {
	log.Println("handling decrypt for", h.partition)

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
	log.Println("handling encrypt for", h.partition)

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
	h.partition = r.GetGetSession().GetPartitionId()

	log.Println("handling get-session for", h.partition)

	s, err := h.sessionFactory.GetSession(h.partition)
	if err != nil {
		return newErrorResponse(err.Error())
	}

	h.session = s

	return new(pb.SessionResponse)
}

func (h *defaultHandler) Close() error {
	log.Println("closing session for", h.partition)
	return h.session.Close()
}

func NewAppEncryption(options *Options) *AppEncryption {
	crypto := aead.NewAES256GCM()

	sf := appencryption.NewSessionFactory(
		&appencryption.Config{
			Service: options.ServiceName,
			Product: options.ProductID,
			Policy:  NewCryptoPolicy(options),
		},
		NewMetastore(options),
		NewKMS(options, crypto),
		crypto,
		appencryption.WithSecretFactory(new(memguard.SecretFactory)),
		appencryption.WithMetrics(false),
	)

	return &AppEncryption{
		streamerFactory: streamerFactoryFunc(func() *streamer {
			return &streamer{sessionFactory: sf}
		}),
	}
}

func NewCryptoPolicy(options *Options) *appencryption.CryptoPolicy {
	policyOpts := []appencryption.PolicyOption{
		appencryption.WithExpireAfterDuration(options.ExpireAfter),
		appencryption.WithRevokeCheckInterval(options.CheckInterval),
	}

	if options.EnableSessionCaching {
		policyOpts = append(policyOpts,
			appencryption.WithSessionCache(),
			appencryption.WithSessionCacheMaxSize(options.SessionCacheMaxSize),
			appencryption.WithSessionCacheDuration(options.SessionCacheDuration),
		)
	}

	return appencryption.NewCryptoPolicy(policyOpts...)
}
