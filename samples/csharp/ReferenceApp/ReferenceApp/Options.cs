using System.Collections.Generic;
using CommandLine;
using static GoDaddy.Asherah.ReferenceApp.ReferenceApp;

namespace GoDaddy.Asherah.ReferenceApp
{
    public class Options
    {
        [Option('m', "metastore-type", Required = false, Default = Metastore.MEMORY, HelpText = "Type of metastore to use. Enum values: MEMORY, ADO, DYNAMODB")]
        public Metastore Metastore { get; set; }

        [Option('e', "dynamodb-endpoint", Required = false, HelpText = "The DynamoDb service endpoint (only supported by DYNAMODB)")]
        public string DynamodbEndpoint { get; set; }

        [Option('r', "dynamodb-region", Required = false, HelpText = "The AWS region for DynamoDB requests (only supported by DYNAMODB)")]
        public string DynamodbRegion { get; set; }

        [Option('t', "dynamodb-table-name", Required = false, HelpText = "The table name for DynamoDb (only supported by DYNAMODB)")]
        public string DynamodbTableName { get; set; }

        [Option('s', "enable-key-suffix", Required = false, HelpText = "Configure the metastore to use key suffixes (only supported by DYNAMODB)")]
        public bool EnableKeySuffix { get; set; }

        [Option('a', "ado-connection-string", Required = false, HelpText = "ADO connection string to use for an ADO metastore. Required for ADO metastore.")]
        public string AdoConnectionString { get; set; }

        [Option('k', "kms-type", Required = false, Default = Kms.STATIC, HelpText = "Type of key management service to use. Enum values: STATIC, AWS")]
        public Kms Kms { get; set; }

        [Option('p', "preferred-region", Required = false, HelpText = "Preferred region to use for KMS if using AWS KMS. Required for AWS KMS.")]
        public string PreferredRegion { get; set; }

        [Option('t', "region-arn-tuples", Required = false, Separator = ',', HelpText = "Comma separated list of <region>=<kms_arn> tuples. Required for AWS KMS.")]
        public IEnumerable<string> RegionToArnTuples { get; set; }

        [Option('i', "iterations", Required = false, HelpText = "Number of encrypt/decrypt iterations to run", Default = 1)]
        public int Iterations { get; set; }

        [Option('c', "enable-cw", Required = false, HelpText = "Enable CloudWatch Metrics output")]
        public bool EnableCloudWatch { get; set; }

        [Option('d', "drr", Required = false, HelpText = "DRR to be decrypted", Default = null)]
        public string Drr { get; set; }
    }
}
