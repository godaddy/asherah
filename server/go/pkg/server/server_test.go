package server

import (
	"context"
	"errors"
	"io"
	"math/rand"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"google.golang.org/grpc/metadata"

	pb "github.com/godaddy/asherah/server/go/api"
)

type mockHandler struct {
	mock.Mock
}

func (m *mockHandler) Decrypt(ctx context.Context, r *pb.SessionRequest) *pb.SessionResponse {
	ret := m.Called(ctx, r)
	return ret.Get(0).(*pb.SessionResponse)
}

func (m *mockHandler) Encrypt(ctx context.Context, r *pb.SessionRequest) *pb.SessionResponse {
	ret := m.Called(ctx, r)
	return ret.Get(0).(*pb.SessionResponse)
}

func (m *mockHandler) GetSession(r *pb.SessionRequest) *pb.SessionResponse {
	ret := m.Called(r)
	return ret.Get(0).(*pb.SessionResponse)
}

func (m *mockHandler) Close() error {
	if len(m.ExpectedCalls) == 0 {
		return nil
	}

	ret := m.Called()

	return ret.Error(0)
}

type mockSessionServer struct {
	mock.Mock
}

func (m *mockSessionServer) Recv() (*pb.SessionRequest, error) {
	ret := m.Called()

	if err := ret.Error(1); err != nil {
		return nil, err
	}

	return ret.Get(0).(*pb.SessionRequest), nil
}

func (m *mockSessionServer) Send(r *pb.SessionResponse) error {
	ret := m.Called(r)

	if err := ret.Error(0); err != nil {
		return err
	}

	return nil
}

func (m *mockSessionServer) SetHeader(md metadata.MD) error {
	ret := m.Called(md)

	return ret.Error(0)
}

func (m *mockSessionServer) SendHeader(md metadata.MD) error {
	ret := m.Called(md)

	return ret.Error(0)
}

func (m *mockSessionServer) SetTrailer(md metadata.MD) {
	m.Called(md)
}

func (m *mockSessionServer) Context() context.Context {
	ret := m.Called()

	return ret.Get(0).(context.Context)
}

func (m *mockSessionServer) SendMsg(msg interface{}) error {
	ret := m.Called(msg)

	return ret.Error(0)
}

func (m *mockSessionServer) RecvMsg(msg interface{}) error {
	ret := m.Called(msg)

	return ret.Error(0)
}

func Test_AppEncryption_Session(t *testing.T) {
	stream := new(mockSessionServer)
	stream.On("Recv").Return(nil, io.EOF)

	sf := new(mockStreamerFactory)
	sf.On("NewStreamer").Return(&streamer{handler: new(mockHandler)})

	ae := &AppEncryption{
		streamerFactory: sf,
	}

	if assert.NoError(t, ae.Session(stream)) {
		mock.AssertExpectationsForObjects(t, stream, sf)
	}
}

func Test_Streamer_StreamEOF(t *testing.T) {
	stream := new(mockSessionServer)
	stream.On("Recv").Return(nil, io.EOF)

	s := new(streamer)
	err := s.Stream(stream)

	assert.NoError(t, err)
}

func Test_Streamer_StreamRecvError(t *testing.T) {
	stream := new(mockSessionServer)
	stream.On("Recv").Return(nil, errors.New("some error"))

	s := new(streamer)
	err := s.Stream(stream)

	assert.EqualError(t, err, "some error")
}

func Test_Streamer_StreamSendError(t *testing.T) {
	req := &pb.SessionRequest{
		Request: new(pb.SessionRequest_Decrypt),
	}
	resp := &pb.SessionResponse{
		Response: new(pb.SessionResponse_DecryptResponse),
	}

	ctx := context.Background()

	stream := new(mockSessionServer)
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Send", resp).Return(errors.New("send error"))
	stream.On("Context").Return(ctx)

	m := new(mockHandler)
	m.On("Decrypt", ctx, req).Return(resp)
	m.On("Close").Return(nil)

	s := &streamer{handler: m}
	err := s.Stream(stream)

	assert.EqualError(t, err, "send error")
	mock.AssertExpectationsForObjects(t, m, stream)
}

func Test_Streamer_StreamDecrypt(t *testing.T) {
	req := &pb.SessionRequest{
		Request: new(pb.SessionRequest_Decrypt),
	}
	expected := &pb.SessionResponse{
		Response: new(pb.SessionResponse_DecryptResponse),
	}

	ctx := context.Background()

	stream := new(mockSessionServer)
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", expected).Return(nil)
	stream.On("Context").Return(ctx)

	m := new(mockHandler)
	m.On("Decrypt", ctx, req).Return(expected)
	m.On("Close").Return(nil)

	s := &streamer{handler: m}
	err := s.Stream(stream)

	assert.NoError(t, err)
	mock.AssertExpectationsForObjects(t, m, stream)
}

func Test_Streamer_StreamDecrypt_BeforeGetSession(t *testing.T) {
	req := &pb.SessionRequest{
		Request: new(pb.SessionRequest_Decrypt),
	}

	stream := new(mockSessionServer)
	stream.On("Context").Return(context.Background())
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", mock.Anything).Return(nil).Run(func(args mock.Arguments) {
		resp := args.Get(0).(*pb.SessionResponse)
		errResp := resp.GetErrorResponse()
		if assert.NotNil(t, errResp) {
			assert.Equal(t, UninitializedSessionResponse, resp)
		}
	})

	s := new(streamer)
	err := s.Stream(stream)

	assert.NoError(t, err)
	mock.AssertExpectationsForObjects(t, stream)
}

func Test_Streamer_StreamEncrypt(t *testing.T) {
	req := &pb.SessionRequest{
		Request: new(pb.SessionRequest_Encrypt),
	}
	expected := &pb.SessionResponse{
		Response: new(pb.SessionResponse_EncryptResponse),
	}

	ctx := context.Background()

	stream := new(mockSessionServer)
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", expected).Return(nil)
	stream.On("Context").Return(ctx)

	m := new(mockHandler)
	m.On("Encrypt", ctx, req).Return(expected)
	m.On("Close").Return(nil)

	s := &streamer{handler: m}
	err := s.Stream(stream)

	assert.NoError(t, err)
	mock.AssertExpectationsForObjects(t, m, stream)
}

func Test_Streamer_StreamEncrypt_BeforeGetSession(t *testing.T) {
	req := &pb.SessionRequest{
		Request: new(pb.SessionRequest_Encrypt),
	}

	stream := new(mockSessionServer)
	stream.On("Context").Return(context.Background())
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", mock.Anything).Return(nil).Run(func(args mock.Arguments) {
		resp := args.Get(0).(*pb.SessionResponse)
		errResp := resp.GetErrorResponse()
		if assert.NotNil(t, errResp) {
			assert.Equal(t, UninitializedSessionResponse, resp)
		}
	})

	s := new(streamer)
	err := s.Stream(stream)

	assert.NoError(t, err)
	mock.AssertExpectationsForObjects(t, stream)
}

func Test_Streamer_StreamGetSession_SessionAlreadyInitializedError(t *testing.T) {
	req := &pb.SessionRequest{
		Request: new(pb.SessionRequest_GetSession),
	}

	stream := new(mockSessionServer)
	stream.On("Context").Return(context.Background())
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", mock.Anything).Return(nil).Run(func(args mock.Arguments) {
		resp := args.Get(0).(*pb.SessionResponse)
		errResp := resp.GetErrorResponse()
		if assert.NotNil(t, errResp) {
			assert.Equal(t, SessionAlreadyInitializedResponse, resp)
		}
	})

	s := &streamer{handler: new(mockHandler)}
	err := s.Stream(stream)

	assert.NoError(t, err)
	mock.AssertExpectationsForObjects(t, stream)
}

type mockStreamerFactory struct {
	mock.Mock
}

func (m *mockStreamerFactory) NewStreamer() *streamer {
	ret := m.Called()

	return ret.Get(0).(*streamer)
}

type mockHandlerFactory struct {
	mock.Mock
}

func (m *mockHandlerFactory) NewHandler() requestHandler {
	ret := m.Called()

	return ret.Get(0).(requestHandler)
}

func Test_Streamer_StreamGetSession(t *testing.T) {
	req := &pb.SessionRequest{
		Request: new(pb.SessionRequest_GetSession),
	}

	resp := new(pb.SessionResponse)

	stream := new(mockSessionServer)
	stream.On("Context").Return(context.Background())
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", resp).Return(nil)

	handler := new(mockHandler)
	handler.On("GetSession", req).Return(resp)
	handler.On("Close").Return(nil)

	m := new(mockHandlerFactory)
	m.On("NewHandler").Return(handler)

	s := &streamer{
		handlerFactory: m,
	}
	err := s.Stream(stream)

	assert.NoError(t, err)
	mock.AssertExpectationsForObjects(t, stream, handler, m)
}

func Test_NewAppEncryption(t *testing.T) {
	// A simple smoke test that consturcts new handlers
	// using a variety of configuration options.
	optCombos := []*Options{
		{KMS: "aws", Metastore: "rdbms"},
		{KMS: "aws", Metastore: "dynamodb"},
		{KMS: "aws", Metastore: "dynamodb", DynamoDBRegion: "us-east-1"},
		{KMS: "aws", Metastore: "dynamodb", DynamoDBEndpoint: "http://localhost:8000"},
		{KMS: "aws", Metastore: "dynamodb", DynamoDBTableName: "CustomTableName"},
		{KMS: "static", Metastore: "rdbms"},
		{KMS: "static", Metastore: "memory"},
	}

	for _, opts := range optCombos {
		opts := opts
		name := opts.KMS + ":" + opts.Metastore
		t.Run(name, func(tt *testing.T) {
			ae := NewAppEncryption(opts)
			s := ae.NewStreamer()
			h := s.NewHandler()

			assert.IsType(tt, (*defaultHandler)(nil), h)
		})
	}
}

func Test_NewCryptoPolicy(t *testing.T) {
	r := rand.New(rand.NewSource(time.Now().UnixNano()))
	opts := &Options{
		ExpireAfter:   time.Minute * time.Duration(r.Intn(10)),
		CheckInterval: time.Second * time.Duration(r.Intn(10)),
	}

	expected := appencryption.NewCryptoPolicy()
	expected.ExpireKeyAfter = opts.ExpireAfter
	expected.RevokeCheckInterval = opts.CheckInterval

	assert.Equal(t, expected, NewCryptoPolicy(opts))
}

func Test_NewCryptoPolicy_WithSessionCache(t *testing.T) {
	r := rand.New(rand.NewSource(time.Now().UnixNano()))
	opts := &Options{
		ExpireAfter:          time.Minute * time.Duration(r.Intn(10)),
		CheckInterval:        time.Second * time.Duration(r.Intn(10)),
		EnableSessionCaching: true,
		SessionCacheMaxSize:  r.Intn(1000),
		SessionCacheDuration: time.Minute * time.Duration(r.Intn(5)),
	}

	expected := appencryption.NewCryptoPolicy()
	expected.ExpireKeyAfter = opts.ExpireAfter
	expected.RevokeCheckInterval = opts.CheckInterval
	expected.CacheSessions = opts.EnableSessionCaching
	expected.SessionCacheMaxSize = opts.SessionCacheMaxSize
	expected.SessionCacheDuration = opts.SessionCacheDuration

	assert.Equal(t, expected, NewCryptoPolicy(opts))
}

type mockSessionFactory struct {
	mock.Mock
}

func (m *mockSessionFactory) GetSession(id string) (*appencryption.Session, error) {
	ret := m.Called(id)
	if err := ret.Error(1); err != nil {
		return nil, err
	}

	return ret.Get(0).(*appencryption.Session), nil
}

func Test_DefaultHandler_GetSession(t *testing.T) {
	id := "partitionId-1"
	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_GetSession{
			GetSession: &pb.GetSession{PartitionId: id},
		},
	}

	m := new(mockSessionFactory)
	session := new(appencryption.Session)
	m.On("GetSession", id).Return(session, nil)

	h := &defaultHandler{
		sessionFactory: m,
	}
	resp := h.GetSession(req)

	assert.NotNil(t, resp)
	assert.Nil(t, resp.Response)
	assert.Equal(t, session, h.session)
	assert.Equal(t, id, h.partition)
	m.AssertExpectations(t)
}

type mockSession struct {
	mock.Mock
}

func (m *mockSession) Close() error {
	ret := m.Called()
	return ret.Error(0)
}

func (m *mockSession) EncryptContext(
	ctx context.Context,
	data []byte,
) (*appencryption.DataRowRecord, error) {
	ret := m.Called(ctx, data)

	if err := ret.Error(1); err != nil {
		return nil, err
	}

	return ret.Get(0).(*appencryption.DataRowRecord), nil
}

func (m *mockSession) DecryptContext(
	ctx context.Context,
	d appencryption.DataRowRecord,
) ([]byte, error) {
	ret := m.Called(ctx, d)

	if err := ret.Error(1); err != nil {
		return nil, err
	}

	return ret.Get(0).([]byte), nil
}

func Test_DefaultHandler_Close(t *testing.T) {
	m := new(mockSession)
	m.On("Close").Return(nil)

	h := &defaultHandler{
		session: m,
	}
	if assert.NoError(t, h.Close()) {
		m.AssertExpectations(t)
	}
}

func Test_DefaultHandler_Encrypt(t *testing.T) {
	data := []byte(`somesupersecretdata`)
	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_Encrypt{
			Encrypt: &pb.Encrypt{
				Data: data,
			},
		},
	}

	ctx := context.Background()
	drr := newAEDRR()

	m := new(mockSession)
	m.On("EncryptContext", ctx, data).Return(drr, nil)

	h := &defaultHandler{
		session: m,
	}

	resp := h.Encrypt(ctx, req)

	assert.Equal(t, toProtobufDRR(drr), resp.GetEncryptResponse().DataRowRecord)

	m.AssertExpectations(t)
}

func newAEDRR() *appencryption.DataRowRecord {
	drr := &appencryption.DataRowRecord{
		Data: []byte("some encrypted data"),
		Key: &appencryption.EnvelopeKeyRecord{
			EncryptedKey: []byte("an encrypted key"),
			Created:      time.Now().Add(-1 * time.Hour).Unix(),
			ParentKeyMeta: &appencryption.KeyMeta{
				ID:      "parent key id",
				Created: time.Now().Unix(),
			},
		},
	}

	return drr
}

func Test_DefaultHandler_EncryptError(t *testing.T) {
	data := []byte(`somesupersecretdata`)
	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_Encrypt{
			Encrypt: &pb.Encrypt{
				Data: data,
			},
		},
	}

	ctx := context.Background()

	m := new(mockSession)
	m.On("EncryptContext", ctx, data).Return(nil, errors.New("encryption error"))

	h := &defaultHandler{
		session: m,
	}

	resp := h.Encrypt(ctx, req)

	respErr := resp.GetErrorResponse()
	if assert.NotNil(t, respErr) {
		assert.Equal(t, "encryption error", respErr.GetMessage())
	}

	m.AssertExpectations(t)
}

func Test_DefaultHandler_Decrypt(t *testing.T) {
	drrAE := newAEDRR()
	drr := toProtobufDRR(drrAE)

	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_Decrypt{
			Decrypt: &pb.Decrypt{
				DataRowRecord: drr,
			},
		},
	}

	ctx := context.Background()

	decryptedData := []byte(`some decrypted data`)

	m := new(mockSession)
	m.On("DecryptContext", ctx, *drrAE).Return(decryptedData, nil)

	h := &defaultHandler{
		session: m,
	}

	resp := h.Decrypt(ctx, req)

	assert.Equal(t, decryptedData, resp.GetDecryptResponse().GetData())
	m.AssertExpectations(t)
}

func Test_DefaultHandler_DecryptError(t *testing.T) {
	drrAE := newAEDRR()
	drr := toProtobufDRR(drrAE)

	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_Decrypt{
			Decrypt: &pb.Decrypt{
				DataRowRecord: drr,
			},
		},
	}

	ctx := context.Background()

	m := new(mockSession)
	m.On("DecryptContext", ctx, *drrAE).Return(nil, errors.New("decryption error"))

	h := &defaultHandler{
		session: m,
	}

	resp := h.Decrypt(ctx, req)

	respErr := resp.GetErrorResponse()
	if assert.NotNil(t, respErr) {
		assert.Equal(t, "decryption error", respErr.GetMessage())
	}

	m.AssertExpectations(t)
}
