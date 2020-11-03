module github.com/godaddy/asherah/go/appencryption/cmd/example

go 1.13

require (
	github.com/TylerBrock/colorjson v0.0.0-20180527164720-95ec53f28296
	github.com/aws/aws-sdk-go v1.34.3
	github.com/fatih/color v1.7.0 // indirect
	github.com/go-sql-driver/mysql v1.5.0
	github.com/godaddy/asherah/go/appencryption v0.0.0-20200318173103-9ec8c9007963
	github.com/godaddy/asherah/go/securememory v0.1.2
	github.com/hokaccha/go-prettyjson v0.0.0-20180920040306-f579f869bbfe // indirect
	github.com/jessevdk/go-flags v1.4.0
	github.com/logrusorgru/aurora v0.0.0-20190803045625-94edacc10f9b
	github.com/mattn/go-colorable v0.1.1 // indirect
	github.com/pkg/errors v0.9.1
	github.com/rcrowley/go-metrics v0.0.0-20181016184325-3113b8401b8a
)

replace github.com/godaddy/asherah/go/appencryption => ../..

replace github.com/godaddy/asherah/go/securememory => ../../../securememory
