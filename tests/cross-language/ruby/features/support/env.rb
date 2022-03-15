require 'asherah'
require 'base64'

SERVICE_NAME      = ENV.fetch('ASHERAH_SERVICE_NAME')
PRODUCT_ID        = ENV.fetch('ASHERAH_PRODUCT_NAME')
KMS               = ENV.fetch('ASHERAH_KMS_MODE')
DB_NAME           = ENV.fetch('TEST_DB_NAME')
DB_USER           = ENV.fetch('TEST_DB_USER')
DB_PASS           = ENV.fetch('TEST_DB_PASSWORD')
DB_PORT           = ENV.fetch('TEST_DB_PORT')
DB_HOST           = 'localhost'
CONNECTION_STRING = "#{DB_USER}:#{DB_PASS}@tcp(#{DB_HOST}:#{DB_PORT})/#{DB_NAME}?tls=skip-verify"
TMP_DIR           = '/tmp/'
FILE_NAME         = 'ruby_encrypted'
METASTORE         = 'rdbms'

Before do |scenario|
  Asherah.configure do |config|
    config.service_name = SERVICE_NAME
    config.product_id = PRODUCT_ID
    config.metastore = METASTORE
    config.connection_string = CONNECTION_STRING
    config.kms = KMS
    config.enable_session_caching = true
    config.verbose = false
  end
end

After do |scenario|
  Asherah.shutdown
end
