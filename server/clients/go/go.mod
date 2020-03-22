module github.com/godaddy/asherah/apps/server/clients/go

go 1.13

replace github.com/godaddy/asherah/server/go => ../../go

require (
	github.com/godaddy/asherah/server/go v0.0.0-00010101000000-000000000000
	github.com/jessevdk/go-flags v1.4.0
	google.golang.org/grpc v1.28.0
)
