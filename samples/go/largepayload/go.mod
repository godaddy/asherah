module largepayload

go 1.25.0

require github.com/godaddy/asherah/go/appencryption v0.9.0

require (
	github.com/awnumar/memcall v0.5.0 // indirect
	github.com/awnumar/memguard v0.23.0 // indirect
	github.com/aws/aws-sdk-go v1.55.8 // indirect
	github.com/godaddy/asherah/go/securememory v0.1.7 // indirect
	github.com/jmespath/go-jmespath v0.4.0 // indirect
	github.com/pkg/errors v0.9.1 // indirect
	github.com/rcrowley/go-metrics v0.0.0-20250401214520-65e299d6c5c9 // indirect
	golang.org/x/crypto v0.48.0 // indirect
	golang.org/x/sys v0.41.0 // indirect
)

replace github.com/godaddy/asherah/go/appencryption => ../../../go/appencryption

replace github.com/godaddy/asherah/go/securememory => ../../../go/securememory
