package main

import (
    "encoding/json"
    "fmt"
    "log"
    "net/http"
    _ "net/http/pprof"
    "os"
    "os/signal"
    "runtime"
    "runtime/pprof"
    "strconv"
    "strings"
    "sync"
    "syscall"
    "time"

    "github.com/aws/aws-sdk-go/aws/session"
    "github.com/godaddy/asherah/go/securememory"
    "github.com/godaddy/asherah/go/securememory/memguard"
    "github.com/jessevdk/go-flags"
    "github.com/logrusorgru/aurora"
    "github.com/pkg/errors"
    "github.com/rcrowley/go-metrics"

    "github.com/godaddy/asherah/go/appencryption"
    "github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
    "github.com/godaddy/asherah/go/appencryption/pkg/kms"
    "github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

const (
	shopperID = "123456"
	dynamodb  = "DYNAMODB"
	rdbms     = "RDBMS"
	awskms    = "AWS"
)

type Options struct {
	Count            int           `short:"c" long:"count" default:"1000" description:"Number of loops to run per session."`
	Sessions         int           `short:"s" long:"sessions" default:"20" description:"Number of sessions to run concurrently."`
	EnableLogs       bool          `short:"l" long:"log" description:"Enables logging to stdout"`
	Results          bool          `short:"r" long:"results" description:"Prints input/output from asherah library"`
	Metrics          bool          `short:"m" long:"metrics" description:"Dumps metrics to stdout in JSON format"`
	Duration         time.Duration `short:"d" long:"duration" description:"Time to run tests for. If not provided, the app will run (sessions X count) then exit"`
	Verbose          bool          `short:"v" long:"verbose" description:"Enables verbose output"`
	ShowAll          bool          `short:"a" long:"all" description:"Print all metrics even if they were not executed."`
	Progress         bool          `short:"P" long:"progress" description:"Prints progress messages while running."`
	Metastore        string        `long:"metastore" description:"Configure what metastore to use"`
	NoCache          bool          `long:"no-cache" description:"Disables the caching of keys"`
	KMS              string        `long:"kms" description:"Configure what kms service to use"`
	Region           string        `long:"region" description:"Describe the preferred region to use"`
	RegionMap        string        `long:"map" description:"Comma separated list of <region>=<kms_arn> tuples."`
	Profile          string        `long:"profile" choice:"cpu" choice:"mem" choice:"mutex" choice:"http"`
	Truncate         bool          `long:"truncate" description:"Deletes all keys present in the database before running."`
	ExpireAfter      time.Duration `long:"expire" description:"Amount of time before a key is expired"`
	CheckInterval    time.Duration `long:"check" description:"Interval to check for expired keys"`
	ConnectionString string        `short:"C" long:"conn" description:"MySQL Connection String"`
	NoExit           bool          `short:"x" long:"no-exit" description:"Prevent app from closing once tests are completed. Especially useful for profiling."`
}

var (
	opts         Options
	encryptTimer = metrics.NewTimer()
	decryptTimer = metrics.NewTimer()
)

func init() {
	metrics.RegisterRuntimeMemStats(metrics.DefaultRegistry)
	metrics.RegisterDebugGCStats(metrics.DefaultRegistry)

	go metrics.CaptureDebugGCStats(metrics.DefaultRegistry, time.Second*1)
	go metrics.CaptureRuntimeMemStats(metrics.DefaultRegistry, time.Second*1)
}

func EncryptAndStore(s *appencryption.Session, b []byte) *appencryption.DataRowRecord {

	dr, err := s.Encrypt(b)
	if err != nil {
		panic(err)
	}

	return dr
}

func GenerateData(session *appencryption.Session, count int) []appencryption.DataRowRecord {
	rows := []appencryption.DataRowRecord{}

	for i := 0; i < count; i++ {
		c := NewContact()
		b, err := json.Marshal(c)
		if err != nil {
			panic(err)
		}

		var dr *appencryption.DataRowRecord

		if opts.Results {
			PrintColoredJSON("Before EncryptAndStore:", c)
		}

		encryptTimer.Time(func() {
			dr = EncryptAndStore(session, b)
		})

		if opts.Results {
			PrintColoredJSON("After EncryptAndStore:", *dr)
		}

		rows = append(rows, *dr)
	}

	return rows
}

func Decrypt(session *appencryption.Session, drr appencryption.DataRowRecord) {

	result, err := session.Decrypt(drr)
	if err != nil {
		panic(err)
	}

	if opts.Results {
		PrintColoredJSON("After Decrypt:", result)
	}
}

func CreatePerfFile() *os.File {
	file, err := os.Create(fmt.Sprintf("%s.out", opts.Profile))
	if err != nil {
		panic(err)
	}

	return file
}

func main() {
	f, err := flags.Parse(&opts)
	if err != nil {
		if e, ok := err.(*flags.Error); ok && e.Type == flags.ErrHelp {
			return
		}

		panic(err)
	}

	if opts.Verbose && len(f) > 0 {
		fmt.Println(aurora.Cyan("Flags:"))
		for _, flagV := range f {
			fmt.Println(flagV)
		}
	}

	if opts.Profile == "http" {
		log.Printf("Starting pprof endpoint")
		go func() {
			log.Println(http.ListenAndServe("localhost:6060", nil))
		}()
	}

	if opts.Profile == "cpu" {
		log.Printf("Writing CPU profile")

		f := CreatePerfFile()
		defer f.Close()

		pprof.StartCPUProfile(f)
		defer pprof.StopCPUProfile()
	}

	crypto := aead.NewAES256GCM()

	var (
		expireAfter   = appencryption.DefaultExpireAfter
		checkInterval = appencryption.DefaultRevokedCheckInterval
	)

	if opts.ExpireAfter > 0 {
		expireAfter = opts.ExpireAfter
	}

	if opts.CheckInterval > 0 {
		checkInterval = opts.CheckInterval
	}

	withCacheOption := func() appencryption.PolicyOption {
		if opts.NoCache {
			return appencryption.WithNoCache()
		}

		return func(*appencryption.CryptoPolicy) { /* noop */ }
	}

	factory := appencryption.NewSessionFactory(
		&appencryption.Config{
			Service: "exampleService",
			Product: "productId",
			Policy: appencryption.NewCryptoPolicy(
				appencryption.WithExpireAfterDuration(expireAfter),
				appencryption.WithRevokeCheckInterval(checkInterval),
				withCacheOption(),
			),
		},
		CreateMetastore(),
		CreateKMS(),
		crypto,
		// optional step(s)
		appencryption.WithSecretFactory(new(memguard.SecretFactory)),
		// appencryption.WithSecretFactory(new(protectedmemory.SecretFactory)),
		appencryption.WithMetrics(false),
	)
	defer factory.Close()

	var wg sync.WaitGroup
	done := make(chan bool, 1)

	start := time.Now()

	if opts.Duration > 0 && opts.Progress {
		go func() {
			totalTime := start.Add(opts.Duration)
			ticker := time.NewTicker(time.Second * 1)

			for {
				left := time.Until(totalTime)
				left = left.Round(time.Second)

				h := left / time.Hour
				left -= h * time.Hour

				m := left / time.Minute
				left -= m * time.Minute

				s := left / time.Second

				fmt.Printf("\rCompleted %d. Time left: %dh:%dm:%ds", encryptTimer.Count(), h, m, s)

				select {
				case <-done:
					fmt.Printf("\n\r\n")
					fmt.Println()
					return
				case <-ticker.C:
					continue
				}
			}
		}()
	}

	for i := 0; i < opts.Sessions; i++ {
		wg.Add(1)

		go func(i int) {
			defer wg.Done()

			runFunc := func(shopper string) {
				session, err := factory.GetSession(shopper)
				if err != nil {
					panic(err)
				}

				defer session.Close()

				dr := GenerateData(session, opts.Count)

				for i := 0; i < len(dr); i++ {
					decryptTimer.Time(func() {
						Decrypt(session, dr[i])
					})
				}
			}

			if opts.Duration > 0 {
				log.Printf("Running for %s", opts.Duration)

				for time.Since(start) < opts.Duration {
					shopper := shopperID + strconv.Itoa(i)

					log.Printf("Starting session with shopper ID: %s", shopper)

					runFunc(shopper)
				}
			} else {
				shopper := shopperID + strconv.Itoa(i)

				log.Printf("Starting session with shopper ID: %s", shopper)

				runFunc(shopper)
			}
		}(i)
	}

	wg.Wait()
	done <- true
	close(done)

	//factory.Close()
	end := time.Since(start)

	if opts.Metrics {
		fmt.Println("Total time:", end)
		fmt.Println("Secrets allocated:", securememory.AllocCounter.Count())
		PrintMetrics("encryption", encryptTimer)
		PrintMetrics("decryption", decryptTimer)
		PrintColoredJSON("Metrics:", metrics.DefaultRegistry)
	}

	if opts.Profile == "mem" {
		f := CreatePerfFile()
		defer f.Close()

		// ensure latest stats
		runtime.GC()

		log.Printf("Writing heap profile")
		pprof.WriteHeapProfile(f)
	}

	if opts.NoExit {
		sigs := make(chan os.Signal, 1)
		done := make(chan bool, 1)

		signal.Notify(sigs, syscall.SIGINT, syscall.SIGTERM)

		go func() {
			sig := <-sigs
			fmt.Printf("%v received\n", sig)
			done <- true
		}()

		fmt.Println("Refusing to exit as per the no-exit flag (send SIGINT or SIGTERM to close)")
		<-done
		fmt.Println("Exiting")
	}
}

func CreateKMS() appencryption.KeyManagementService {
	crypto := aead.NewAES256GCM()

	if opts.KMS == awskms {

		if opts.Region == "" || opts.RegionMap == "" {
			panic(errors.Errorf("preferred region and <region>=<arn> tuples are mandatory with  KMS Type: AWS"))
		}
		regionArnMap := make(map[string]string)
		splits := strings.Split(opts.RegionMap, ",")
		for _, regionArn := range splits {
			regionArnMap[strings.Split(regionArn, "=")[0]] = strings.Split(regionArn, "=")[1]
		}
		log.Printf("Using AWS KMS...")
		kms, err := kms.NewAWS(crypto, opts.Region, regionArnMap)
		if err != nil {
			panic(err)
		}
		return kms
	}

	log.Printf("Using static KMS...")
	kms, err := kms.NewStatic("thisIsAStaticMasterKeyForTesting", crypto)
	if err != nil {
		panic(errors.Wrap(err, "failed to create static KMS"))
	}
	return kms
}

func CreateMetastore() appencryption.Metastore {

	if opts.Metastore == rdbms {

		if opts.ConnectionString == "" {
			panic(errors.Errorf("Connection string is a mandatory parameter with MetaStore Type: RDBMS"))
		}

		log.Printf("Using mysql metastore...")
		if err := getDB(opts.ConnectionString); err != nil {
			panic(err)
		}

		if opts.Truncate {
			TruncateKeys()
		}

		return persistence.NewSQLMetastore(DB)
	}

	if opts.Metastore == dynamodb {

		log.Printf("Using dyamodb metastore...")
		// Initialize a session that the AWS SDK will use to load
		// credentials from the shared credentials file ~/.aws/credentials
		// and region from the shared configuration file ~/.aws/config.
		sess := session.Must(session.NewSessionWithOptions(session.Options{
			SharedConfigState: session.SharedConfigEnable,
		}))
		return persistence.NewDynamoDBMetastore(sess)
	}

	log.Printf("Using in-memory metastore...")
	return persistence.NewMemoryMetastore()
}
