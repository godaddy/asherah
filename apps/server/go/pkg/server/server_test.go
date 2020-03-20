package server

import (
	"context"
	"errors"
	"io"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"google.golang.org/grpc/metadata"

	pb "github.com/godaddy/asherah/apps/server/go/api"
)

type mockHandler struct {
	mock.Mock
}

func (m *mockHandler) Decrypt(r *pb.SessionRequest) *pb.SessionResponse {
	ret := m.Called(r)
	return ret.Get(0).(*pb.SessionResponse)
}

func (m *mockHandler) Encrypt(r *pb.SessionRequest) *pb.SessionResponse {
	ret := m.Called(r)
	return ret.Get(0).(*pb.SessionResponse)
}

func (m *mockHandler) GetSession(r *pb.SessionRequest) *pb.SessionResponse {
	ret := m.Called(r)
	return ret.Get(0).(*pb.SessionResponse)
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

func Test_Streamer_NewHandler(t *testing.T) {
	s := new(streamer)
	h := s.NewHandler()

	assert.IsType(t, (*defaultHandler)(nil), h)
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

	stream := new(mockSessionServer)
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Send", resp).Return(errors.New("send error"))

	m := new(mockHandler)
	m.On("Decrypt", req).Return(resp)

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

	stream := new(mockSessionServer)
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", expected).Return(nil)

	m := new(mockHandler)
	m.On("Decrypt", req).Return(expected)

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
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", mock.Anything).Return(nil).Run(func(args mock.Arguments) {
		resp := args.Get(0).(*pb.SessionResponse)
		errResp := resp.GetErrorResponse()
		if assert.NotNil(t, errResp) {
			assert.Equal(t, UnitializedSessionResponse, resp)
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

	stream := new(mockSessionServer)
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", expected).Return(nil)

	m := new(mockHandler)
	m.On("Encrypt", req).Return(expected)

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
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", mock.Anything).Return(nil).Run(func(args mock.Arguments) {
		resp := args.Get(0).(*pb.SessionResponse)
		errResp := resp.GetErrorResponse()
		if assert.NotNil(t, errResp) {
			assert.Equal(t, UnitializedSessionResponse, resp)
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
	stream.On("Recv").Return(req, nil).Once()
	stream.On("Recv").Return(nil, io.EOF)
	stream.On("Send", resp).Return(nil)

	handler := new(mockHandler)
	handler.On("GetSession", req).Return(resp)

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
	ae := NewAppEncryption()
	assert.NotNil(t, ae)
}

func Test_DefaultHandler_GetSession(t *testing.T) {
	id := "partitionId-1"
	req := &pb.SessionRequest{
		Request: &pb.SessionRequest_GetSession{
			GetSession: &pb.GetSession{PartitionId: id},
		},
	}

	s := &defaultHandler{}
	resp := s.GetSession(req)

	assert.NotNil(t, resp)
}
