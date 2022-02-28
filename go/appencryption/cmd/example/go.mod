module github.com/godaddy/asherah/go/appencryption/cmd/example

go 1.13

require (
	github.com/TylerBrock/colorjson v0.0.0-20200706003622-8a50f05110d2
	github.com/aws/aws-sdk-go v1.43.7
	github.com/fatih/color v1.13.0 // indirect
	github.com/go-sql-driver/mysql v1.6.0
	github.com/godaddy/asherah/go/appencryption v0.2.3
	github.com/godaddy/asherah/go/securememory v0.1.2
	github.com/hokaccha/go-prettyjson v0.0.0-20211117102719-0474bc63780f // indirect
	github.com/jessevdk/go-flags v1.5.0
	github.com/logrusorgru/aurora v2.0.3+incompatible
	github.com/mattn/go-colorable v0.1.12 // indirect
	github.com/pkg/errors v0.9.1
	github.com/rcrowley/go-metrics v0.0.0-20201227073835-cf1acfcdf475
)

replace github.com/godaddy/asherah/go/appencryption => ../..

replace github.com/godaddy/asherah/go/securememory => ../../../securememory
