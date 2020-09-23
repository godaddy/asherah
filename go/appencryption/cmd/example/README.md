# Encryption Performance Test App

## Usage

```bash
Usage:
  example [OPTIONS]

Application Options:
  -c, --count=                  Number of loops to run per session. (default: 1000)
  -i, --iterations=             Number of times each session loop will run. (default: 1)
  -s, --sessions=               Number of sessions to run concurrently. (default: 20)
  -p, --perf                    Print performance metrics
  -l, --log                     Enables logging to stdout
  -S, --session-cache           Enables the shared session cache.
  -r, --results                 Prints input/output from encryption library
  -m, --metrics                 Dumps metrics to stdout in JSON format
  -d, --duration=               Time to run tests for. If not provided, the app will run (sessions X count) then exit
  -v, --verbose                 Enables verbose output
  -a, --all                     Print all metrics even if they were not executed.
  -P, --progress                Prints progress messages while running.
      --metastore               Select metastore type (LOCAL/RDBMS)
  -C, --conn=                   MySQL Connection String (mandatory when using RDBMS)
      --kms                     Select kms type (STATIC/AWS)
      --region                  Define the preferred AWS region (mandatory when using aws kms)
      --map                     Comma separated list of <region>=<kms_arn> tuples (mandatory when using aws kms)
      --profile=[cpu|mem|mutex]
      --truncate                Deletes all keys present in the database before running.
      --expire=                 Amount of time before a key is expired
      --check=                  Interval to check for expired keys

Help Options:
  -h, --help                    Show this help message
```

### Example Usage:

- Encrypt and decrypt a single record using static KMS and in memory metastore
```go
go run . -s 1 -c 1 -r
```
- Run performance timers for X duration and disable progress
```go
go run . -d 10s -p -P
```
- Use an RDBMS metastore
```go
go run . -s 1 -c 1 --metastore RDBMS -C user:password@tcp(mysqlhost:3306)/databasename
```

- Use DynamoDB metastore
```go
# Make sure that you have ~/.aws/credentials and ~/.aws/config
go run . -s 1 -c 1 --metastore DYNAMODB

```
- Use AWS KMS
```go
go run . -s 1 -c 1 --kms AWS --region us-west-2 --map region1=arn_of_kms_key_for_region1,region2=arn_of_kms_key_for_region2
```
