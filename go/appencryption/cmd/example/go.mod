module github.com/godaddy/asherah/go/appencryption/cmd/example

go 1.19

require (
	github.com/TylerBrock/colorjson v0.0.0-20200706003622-8a50f05110d2
	github.com/aws/aws-sdk-go v1.44.155
	github.com/go-sql-driver/mysql v1.6.0
	github.com/godaddy/asherah/go/appencryption v0.2.4
	github.com/godaddy/asherah/go/securememory v0.1.3
	github.com/jessevdk/go-flags v1.5.0
	github.com/logrusorgru/aurora v2.0.3+incompatible
	github.com/pkg/errors v0.9.1
	github.com/rcrowley/go-metrics v0.0.0-20201227073835-cf1acfcdf475
)

require (
	github.com/awnumar/memcall v0.1.2 // indirect
	github.com/awnumar/memguard v0.22.3 // indirect
	github.com/fatih/color v1.13.0 // indirect
	github.com/goburrow/cache v0.1.4 // indirect
	github.com/hokaccha/go-prettyjson v0.0.0-20211117102719-0474bc63780f // indirect
	github.com/jmespath/go-jmespath v0.4.0 // indirect
	github.com/mattn/go-colorable v0.1.12 // indirect
	github.com/mattn/go-isatty v0.0.14 // indirect
	golang.org/x/crypto v0.0.0-20221005025214-4161e89ecf1b // indirect
	golang.org/x/sys v0.2.0 // indirect
	gopkg.in/yaml.v2 v2.4.0 // indirect
)

replace github.com/godaddy/asherah/go/appencryption => ../..

replace github.com/godaddy/asherah/go/securememory => ../../../securememory
