module github.com/godaddy/asherah/go/appencryption/cmd/example

go 1.21

toolchain go1.23.0

require (
	github.com/TylerBrock/colorjson v0.0.0-20200706003622-8a50f05110d2
	github.com/go-sql-driver/mysql v1.7.0
	github.com/godaddy/asherah/go/appencryption v0.2.6
	github.com/godaddy/asherah/go/securememory v0.1.5
	github.com/jessevdk/go-flags v1.5.0
	github.com/logrusorgru/aurora v2.0.3+incompatible
	github.com/pkg/errors v0.9.1
	github.com/rcrowley/go-metrics v0.0.0-20201227073835-cf1acfcdf475
)

require (
	github.com/awnumar/memcall v0.1.2 // indirect
	github.com/awnumar/memguard v0.22.3 // indirect
	github.com/aws/aws-sdk-go v1.51.21 // indirect
	github.com/aws/aws-sdk-go-v2 v1.30.4 // indirect
	github.com/aws/aws-sdk-go-v2/config v1.27.27 // indirect
	github.com/aws/aws-sdk-go-v2/credentials v1.17.27 // indirect
	github.com/aws/aws-sdk-go-v2/feature/dynamodb/attributevalue v1.14.11 // indirect
	github.com/aws/aws-sdk-go-v2/feature/dynamodb/expression v1.7.33 // indirect
	github.com/aws/aws-sdk-go-v2/feature/ec2/imds v1.16.11 // indirect
	github.com/aws/aws-sdk-go-v2/internal/configsources v1.3.16 // indirect
	github.com/aws/aws-sdk-go-v2/internal/endpoints/v2 v2.6.16 // indirect
	github.com/aws/aws-sdk-go-v2/internal/ini v1.8.0 // indirect
	github.com/aws/aws-sdk-go-v2/service/dynamodb v1.34.5 // indirect
	github.com/aws/aws-sdk-go-v2/service/dynamodbstreams v1.22.4 // indirect
	github.com/aws/aws-sdk-go-v2/service/internal/accept-encoding v1.11.4 // indirect
	github.com/aws/aws-sdk-go-v2/service/internal/endpoint-discovery v1.9.17 // indirect
	github.com/aws/aws-sdk-go-v2/service/internal/presigned-url v1.11.17 // indirect
	github.com/aws/aws-sdk-go-v2/service/kms v1.35.3 // indirect
	github.com/aws/aws-sdk-go-v2/service/sso v1.22.4 // indirect
	github.com/aws/aws-sdk-go-v2/service/ssooidc v1.26.4 // indirect
	github.com/aws/aws-sdk-go-v2/service/sts v1.30.3 // indirect
	github.com/aws/smithy-go v1.20.4 // indirect
	github.com/fatih/color v1.13.0 // indirect
	github.com/hokaccha/go-prettyjson v0.0.0-20211117102719-0474bc63780f // indirect
	github.com/jmespath/go-jmespath v0.4.0 // indirect
	github.com/mattn/go-colorable v0.1.12 // indirect
	github.com/mattn/go-isatty v0.0.16 // indirect
	golang.org/x/crypto v0.22.0 // indirect
	golang.org/x/sys v0.19.0 // indirect
)

replace github.com/godaddy/asherah/go/appencryption => ../..

replace github.com/godaddy/asherah/go/securememory => ../../../securememory
