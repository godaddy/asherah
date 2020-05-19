<?php

namespace Example;

use Asherah\Apps\Server\AppEncryptionClient;
use Asherah\Apps\Server\DataRowRecord;
use Asherah\Apps\Server\Decrypt;
use Asherah\Apps\Server\Encrypt;
use Asherah\Apps\Server\EnvelopeKeyRecord;
use Asherah\Apps\Server\GetSession;
use Asherah\Apps\Server\KeyMeta;
use Asherah\Apps\Server\SessionRequest;
use Grpc\ChannelCredentials;
use splitbrain\phpcli\CLI;
use splitbrain\phpcli\Options;
use splitbrain\phpcli\Exception;

/**
 * Example Asherah PHP Client.
 */
class Client extends CLI
{
    /**
     * Setup client application options.
     *
     * @param Options $options
     */
    protected function setup(Options $options)
    {
        $options->setHelp('Asherah demo application.');
        $options->registerOption('socket', 'Asherah socket file.', 's', 'unix:///sock/appencryption.sock');
        $options->registerOption('secret', 'Secret text.', 'S', 'this is my secret');
        $options->registerOption('partition_id', 'Partition ID.', 'p', 'partition_id');
    }

    /**
     * Main application.
     *
     * @param Options $options
     */
    protected function main(Options $options)
    {
        $this->info('starting test');
        $socket = $options->getOpt('socket') ?: 'unix:///tmp/appencryption.sock';
        $secret = $options->getOpt('secret') ?: uniqid('my "secret" data - ');
        $partition_id = $options->getOpt('partition_id') ?: uniqid('partitionid-');

        $client = new AppEncryptionClient($socket, [
            'credentials' => ChannelCredentials::createInsecure(),
        ]);

        register_shutdown_function(function () use (&$client) {
            $client->close();
        });

        $session = $client->Session();

        $this->info("starting session for $partition_id");
        $session->write(
            (new SessionRequest())->setGetSession(
                (new GetSession())->setPartitionId($partition_id)
            )
        );
        $response = $session->read();
        if (is_null($response)) {
            throw new Exception('invalid session response', 1);
        }

        $this->info("encrypting: $secret");
        $session->write(
            (new SessionRequest())->setEncrypt(
                (new Encrypt())->setData($secret)
            )
        );

        $response = $session->read();
        if (is_null($response)) {
            throw new Exception('invalid response', 1);
        } elseif ($error = $response->getErrorResponse()) {
            throw new Exception($error->getMessage(), 1);
        }

        $encrypt_response = $response->getEncryptResponse();
        if (is_null($encrypt_response)) {
            throw new Exception('invalid encrypt response', 1);
        }

        $this->info('received DRR');

        $drr = $encrypt_response->getDataRowRecord();
        $ekr = $drr->getKey();
        $pk = $ekr->getParentKeyMeta();

        $data = $drr->getData();
        $key = $ekr->getKey();
        $key_created = $ekr->getCreated();
        $parent_key_id = $pk->getKeyId();
        $parent_key_created = $pk->getCreated();

        $this->info('decrypting DRR');

        $session->write(
            (new SessionRequest())->setDecrypt(
                (new Decrypt())->setDataRowRecord(
                    (new DataRowRecord())->setData($data)->setKey(
                        (new EnvelopeKeyRecord())->setKey($key)->setCreated($key_created)->setParentKeyMeta(
                            (new KeyMeta())->setKeyId($parent_key_id)->setCreated($parent_key_created)
                        )
                    )
                )
            )
        );

        $response = $session->read();
        if (is_null($response)) {
            throw new Exception('invalid response', 1);
        } elseif ($error = $response->getErrorResponse()) {
            throw new Exception($error->getMessage(), 1);
        }

        $decrypt_response = $response->getDecryptResponse();
        if (is_null($decrypt_response)) {
            throw new Exception('invalid decrypt response', 1);
        }

        $data = $decrypt_response->getData();

        $this->info("received decrypted data: $data");

        // verify the data
        if ($secret !== $data) {
            throw new Exception('oh no... something went terribly wrong!', 1);
        }

        $this->info('test completed successfully');
    }
}
